using System;
using System.Net.WebSockets;
using Akka.Actor;
using PureWebSockets;
using Serilog;

namespace CreateAR.Snap
{
    public class ConnectionActor : ReceiveActor
    {
        public class Connect
        {
            public string Url;
        }

        public class Ready
        {
            public string Url;
        }

        private PureWebSocket _socket;

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
            // 
        }

        private void OnConnect(Connect msg)
        {
            Become(Connecting);

            Log.Information("Starting connection to {0}.", msg.Url);

            // start connecting
            _socket = new PureWebSocket(
                msg.Url,
                new PureWebSocketOptions
                {
                    DebugMode = true,
                    SendDelay = 100
                });

            _socket.OnStateChanged += Socket_OnStateChanged;
            _socket.OnMessage += Socket_OnMessage;
            _socket.OnClosed += Socket_OnClosed;
            _socket.OnSendFailed += Socket_OnSendFailed;
            _socket.Connect();
        }

        private void Socket_OnSendFailed(string data, Exception ex)
        {
            Log.Warning("Could not send message: {0} -> {1}.", data, ex);
        }

        private void Socket_OnClosed(WebSocketCloseStatus reason)
        {
            Log.Information("Socket closed : {0}.", reason);
        }

        private void Socket_OnMessage(string message)
        {
            Log.Information("Received message : {0}.", message);
        }

        private void Socket_OnStateChanged(WebSocketState newState, WebSocketState prevState)
        {
            Log.Information("Socket stage change: {0} -> {1}.",
                prevState,
                newState);
            

        }
    }
}