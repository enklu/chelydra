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

        private string _baseUrl;

        private string _orgId;
        private string _instanceId;
        private string _token;

        public ApplicationActor(
            string baseUrl,
            string orgId,
            string instanceId,
            string token)
        {
            _baseUrl = baseUrl;
            _orgId = orgId;
            _instanceId = instanceId;
            _token = token;

            _connection = Context.ActorOf(Props.Create(() => new ConnectionActor()));
            _processor = Context.ActorOf(Props.Create(() => new ImageProcessingPipelineActor(
                _baseUrl,
                _token)));

            Receive<Start>(msg => Become(Started));
        }

        private void Started()
        {
            Log.Information("Starting application.");

            Receive<ConnectionActor.Ready>(msg => OnConnectionReady(msg));
            Receive<ConnectionActor.TakeSnapMessage>(msg =>
            {
                Log.Information("Received TakeSnapMessage.");

                _processor.Tell(new ImageProcessingPipelineActor.StartPipeline
                {
                    Snap = new ImageProcessingPipelineActor.SnapRecord()
                });
            });

            // connect
            _connection.Tell(new ConnectionActor.Connect
            {
                Url = $"{_baseUrl.Replace("http", "ws")}/socket.io/?nosession=true&__sails_io_sdk_version=1.2.1&__sails_io_sdk_platform=browser&__sails_io_sdk_language=javascript&EIO=3&transport=websocket",
                OrgId = _orgId,
                Token = _token,
                Subscriber = Self
            });
        }

        private void OnConnectionReady(ConnectionActor.Ready msg)
        {
            Log.Information("Connection online.");

            // STUB
            /*Context.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                _processor,
                new ImageProcessingPipelineActor.StartPipeline
                {
                    Snap = new ImageProcessingPipelineActor.SnapRecord
                    {
                        OrgId = _orgId,
                        InstanceId = _instanceId
                    }
                },
                null);*/
        }
    }
}
