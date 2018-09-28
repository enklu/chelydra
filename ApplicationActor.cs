using Akka.Actor;
using Serilog;
using System;

namespace CreateAR.Snap
{

    public class ApplicationActor : ReceiveActor
    {
        public class Start
        {
            //
        }

        private IActorRef _connection;
        private IActorRef _processor;

        public ApplicationActor()
        {
            _connection = Context.ActorOf(Props.Create(() => new ConnectionActor()));
            _processor = Context.ActorOf(Props.Create(() => new ImageProcessingPipelineActor()));

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
                InstanceId = "foo",
                UserId = "foo",
                Subscriber = Self
            });
        }

        private void OnConnectionReady(ConnectionActor.Ready msg)
        {
            Log.Information("Connection online.");

            // STUB
            Context.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                _processor,
                new ImageProcessingPipelineActor.StartPipeline
                {
                    Snap = new ImageProcessingPipelineActor.SnapRecord()
                },
                null);
        }
    }
}
