using Akka.Actor;
using Serilog;

namespace CreateAR.Snap
{
    public class ApplicationActor : ReceiveActor
    {
        public class Start
        {
            //
        }

        private IActorRef _connection;

        public ApplicationActor()
        {
            _connection = Context.ActorOf(Props.Create(() => new ConnectionActor()));

            Receive<Start>(msg => Become(Started));
        }

        private void Started()
        {
            Log.Information("Starting application.");

            Receive<ConnectionActor.Ready>(msg => OnConnectionReady(msg));

            // connect
            _connection.Tell(new ConnectionActor.Connect
            {
                Url = "wss://trellis.enklu.com:10001/socket.io/?nosession=true&__sails_io_sdk_version=1.2.1&__sails_io_sdk_platform=browser&__sails_io_sdk_language=javascript&EIO=3&transport=websocket",
                Subscriber = Self
            });
        }

        private void OnConnectionReady(ConnectionActor.Ready msg)
        {
            Log.Information("Connection online.");
        }
    }
}
