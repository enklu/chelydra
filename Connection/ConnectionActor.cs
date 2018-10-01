using System;
using System.Net.WebSockets;
using Akka.Actor;
using PureWebSockets;
using Serilog;
using Newtonsoft.Json;

namespace CreateAR.Snap
{
    /// <summary>
    /// Actor that connects to Trellis.
    /// </summary>
    public partial class ConnectionActor : ReceiveActor
    {
        /// <summary>
        /// Message used to startup connection.
        /// </summary>
        public class Connect
        {
            /// <summary>
            /// The URL.
            /// </summary>
            public string Url;

            /// <summary>
            /// JWT.
            /// </summary>
            public string Token;

            /// <summary>
            /// Organization.
            /// </summary>
            public string OrgId;

            /// <summary>
            /// Listens for updates.
            /// </summary>
            public IActorRef Subscriber;
        }

        /// <summary>
        /// Used internally to track heartbeats.
        /// </summary>
        private class Heartbeat { }

        /// <summary>
        /// Used internally when socket is connected.
        /// </summary>
        private class Socket_Connected { }

        /// <summary>
        /// Used internally when socket has disconnected.
        /// </summary>
        private class Socket_Disconnected { }

        /// <summary>
        /// Used internally to reconnect.
        /// </summary>
        private class Reconnect { }

        /// <summary>
        /// Trellis URL to connect to.
        /// </summary>
        private string _url;

        /// <summary>
        /// JWT.
        /// </summary>
        private string _token;

        /// <summary>
        /// Organization id.
        /// </summary>
        private string _orgId;

        /// <summary>
        /// Actor that wants updates.
        /// </summary>
        private IActorRef _subscriber;

        /// <summary>
        /// Underlying socket.
        /// </summary>
        private PureWebSocket _socket;

        /// <summary>
        /// Allows cancelation of heartbeat.
        /// </summary>
        private ICancelable _heartbeat;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ConnectionActor()
        {
            Become(Waiting);
        }

        /// <summary>
        /// State listening for connect message.
        /// </summary>
        private void Waiting()
        {
            Log.Information("State::Waiting.");
            
            Receive<Connect>(msg => OnConnect(msg));
        }

        /// <summary>
        /// State used while trying to connect.
        /// </summary>
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

            try
            {
                _socket.Connect();
            }
            catch
            {
                // socket close handler will be called
            }
        }

        /// <summary>
        /// State used when successfully subscribed.
        /// </summary>
        private void Subscribed()
        {
            Log.Information("State::Subscribed.");
            
            Receive<Heartbeat>(msg => _socket.Send("40"));
            Receive<Socket_Disconnected>(msg => Become(Disconnected));

            // start the heartbeat
            _heartbeat = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(10),
                Self,
                new Heartbeat(),
                null);

            // tell subscriber we're ready!
            Log.Information("Connection is ready.");
        }

        /// <summary>
        /// State after socket is closed that waits to reconnect.
        /// </summary>
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

        /// <summary>
        /// Sends a message.
        /// </summary>
        /// <param name="request">The request.</param>
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

        /// <summary>
        /// Called to start connecting.
        /// </summary>
        /// <param name="msg">The message.</param>
        private void OnConnect(Connect msg)
        {
            _url = msg.Url;
            _token = msg.Token;
            _orgId = msg.OrgId;
            _subscriber = msg.Subscriber;

            Become(Connecting);
        }

        /// <summary>
        /// Generates a listener for socket close events.
        /// </summary>
        /// <param name="connection">A refernce to the connection.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a listener for socket message events.
        /// </summary>
        /// <param name="subscriber">The object listening.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Generates a listener for socket state change events.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <returns></returns>
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