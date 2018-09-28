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

            public string InstanceId;

            public string UserId;

            public IActorRef Subscriber;
        }

        public class Ready
        {
            public string Url;
        }

        private class Heartbeat
        {
            //
        }

        private class Socket_Connected
        {
            //
        }

        private string _token;

        private string _instanceId;

        private string _userId;

        private IActorRef _subscriber;

        private PureWebSocket _socket;

        private ICancelable _heartbeat;

        public ConnectionActor()
        {
            Become(Waiting);
        }

        private void Waiting()
        {
            Receive<Connect>(msg => OnConnect(msg));
        }

        private void Connecting()
        {
            Receive<Socket_Connected>(msg => Become(Subscribed));
        }

        private void Subscribed()
        {
            // listen for a heartbeat
            Receive<Heartbeat>(msg => _socket.Send("40"));

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
            _token = msg.Token;
            _instanceId = msg.InstanceId;
            _userId = msg.UserId;
            _subscriber = msg.Subscriber;

            Become(Connecting);

            Log.Information("Starting connection to {0}.", msg.Url);

            // start connecting
            _socket = new PureWebSocket(
                msg.Url,
                new PureWebSocketOptions
                {
                    SendDelay = 100
                });

            _socket.OnStateChanged += Socket_OnStateChanged(Self);
            _socket.OnMessage += Socket_OnMessage(_subscriber);
            _socket.OnClosed += Socket_OnClosed(Self);
            _socket.OnSendFailed += Socket_OnSendFailed(Self);
            _socket.Connect();
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

                // TODO: reconnect
            };
        }

        private Message Socket_OnMessage(IActorRef subscriber)
        {
            return (string message) =>
            {
                // ignore heartbeats
                if (message == "40") {
                    return;
                }
                
                Log.Information("Received message : {0}.", message);
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
                        $"/v1/snap/{_instanceId}/{_userId}/subscribe",
                        "post"
                    ));

                    // TODO: wait for response before assuming success
                    connection.Tell(new Socket_Connected());
                }
            };
        }
    }
}