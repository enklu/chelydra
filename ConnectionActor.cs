using System;
using System.Net.WebSockets;
using Akka.Actor;
using PureWebSockets;
using Serilog;
using Newtonsoft.Json;

namespace CreateAR.Snap
{
    public class ConnectionActor : ReceiveActor
    {
        public class Connect
        {
            public string Url;

            public string Token;

            public string OrgId;

            public IActorRef Subscriber;
        }

        public class Ready
        {
            public string Url;
        }

        public class TakeSnapMessage
        {
            [JsonProperty("type")]
            public string Type;

            [JsonProperty("instanceId")]
            public string InstanceId;
        }

        private class Heartbeat
        {
            //
        }

        private class Socket_Connected
        {
            //
        }

        private class Socket_Disconnected
        {
            //
        }

        private class Reconnect
        {
            //
        }

        private string _url;

        private string _token;

        private string _orgId;

        private IActorRef _subscriber;

        private PureWebSocket _socket;

        private ICancelable _heartbeat;

        public ConnectionActor()
        {
            Become(Waiting);
        }

        private void Waiting()
        {
            Log.Information("State::Waiting.");
            
            Receive<Connect>(msg => OnConnect(msg));
        }

        private void Disconnected()
        {
            Log.Information("State::Disconnected.");

            if (null != _heartbeat)
            {
                _heartbeat.Cancel();
                _heartbeat = null;
            }

            Receive<Reconnect>(msg => Become(Connecting));

            Context.System.Scheduler.ScheduleTellOnce(
                TimeSpan.FromSeconds(3),
                Self,
                new Reconnect(),
                null);
        }

        private void Connecting()
        {
            Log.Information("State::Connecting.");

            Receive<Socket_Connected>(msg => Become(Subscribed));
            Receive<Socket_Disconnected>(msg => Become(Disconnected));

            Log.Information("Starting connection to {0}.", _url);

            // start connecting
            _socket = new PureWebSocket(
                _url,
                new PureWebSocketOptions
                {
                    SendDelay = 100,
                    DebugMode = false
                });

            _socket.OnStateChanged += Socket_OnStateChanged(Self);
            _socket.OnMessage += Socket_OnMessage(_subscriber);
            _socket.OnClosed += Socket_OnClosed(Self);
            //_socket.OnSendFailed += Socket_OnSendFailed(Self);

            try
            {
                _socket.Connect();
            }
            catch
            {
                // 
            }
        }

        private void Subscribed()
        {
            Log.Information("State::Subscribed.");
            
            Receive<Heartbeat>(msg => _socket.Send("40"));
            Receive<Socket_Disconnected>(msg => Become(Disconnected));

            // start the heartbeat
            _heartbeat = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(3),
                Self,
                new Heartbeat(),
                null);

            // tell subscriber we're ready!
            _subscriber.Tell(new Ready());
        }

        private void Send(WebSocketRequest request)
        {
            if (null == request.Headers)
            {
                request.Headers = new WebSocketRequest.HeaderData();
            }
            request.Headers.Authorization = $"Bearer {_token}";

            var json = $"42[\"post\", {JsonConvert.SerializeObject(request)}]";
            Log.Information($"Sending {json}.");
            if (!_socket.Send(json))
            {
                Log.Warning("Could not send!");
            }
        }

        private void OnConnect(Connect msg)
        {
            _url = msg.Url;
            _token = msg.Token;
            _orgId = msg.OrgId;
            _subscriber = msg.Subscriber;

            Become(Connecting);
        }

        private SendFailed Socket_OnSendFailed(IActorRef connection)
        {
            return (string data, Exception ex) => {
                Log.Warning("Could not send message: {0} -> {1}.", data, ex);

                // TODO: tell parent
            };
        }

        private Closed Socket_OnClosed(IActorRef connection)
        {
            return (WebSocketCloseStatus reason) => {
                Log.Information("Socket closed : {0}.", reason);

                if (_socket.State == WebSocketState.Open
                    || _socket.State == WebSocketState.CloseReceived
                    || _socket.State == WebSocketState.CloseSent)
                {
                    _socket.Dispose();
                    _socket = null;
                }

                connection.Tell(new Socket_Disconnected());
            };
        }

        private Message Socket_OnMessage(IActorRef subscriber)
        {
            return (string message) =>
            {
                // ignore heartbeats
                if (!message.StartsWith("42")) {
                    return;
                }

                message = message.Replace("42[\"message\",", "");
                message = message.TrimEnd(']');

                Log.Information("Received message : {0}.", message);

                // deserialize and forward
                try
                {
                    var msg = (TakeSnapMessage) JsonConvert.DeserializeObject(
                        message,
                        typeof(TakeSnapMessage));
                    
                    if (msg.Type != "takesnap")
                    {
                        return;
                    }

                    subscriber.Tell(msg);
                }
                catch (Exception exception)
                {
                    Log.Warning($"Could not deserialize message : {message} : {exception}.");
                }
            };
        }

        private StateChanged Socket_OnStateChanged(IActorRef connection)
        {
            return (WebSocketState newState, WebSocketState prevState) => {
                Log.Information("Socket stage change: {0} -> {1}.",
                    prevState,
                    newState);
                
                if (newState == WebSocketState.Open)
                {
                    // subscribe to trellis events
                    Send(new WebSocketRequest(
                        $"/v1/org/{_orgId}/snap/subscribe",
                        "post"
                    ));

                    // wait for response before assuming success
                    connection.Tell(new Socket_Connected());
                }
            };
        }
    }
}