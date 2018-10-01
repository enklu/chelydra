using Akka.Actor;
using Serilog;
using System;

namespace CreateAR.Snap
{
    /// <summary>
    /// The main actor of the application.
    /// </summary>
    public class ApplicationActor : ReceiveActor
    {
        /// <summary>
        /// Reference to the connection to Trellis.
        /// </summary>
        private IActorRef _connection;

        /// <summary>
        /// Reference to the image processor.
        /// </summary>
        private IActorRef _processor;

        /// <summary>
        /// Base URL of Trellis.
        /// </summary>
        private string _baseUrl;

        /// <summary>
        /// Organization id.
        /// </summary>
        private string _orgId;

        /// <summary>
        /// Instance id.
        /// </summary>
        private string _instanceId;

        /// <summary>
        /// Token.
        /// </summary>
        private string _token;

        /// <summary>
        /// Constructor.
        /// </summary>
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

            Log.Information("Starting application.");

            // listen for the connection being ready
            Receive<ConnectionActor.Ready>(msg => Log.Information("Connection online."));

            // listen for the connection telling us to take a snapshot
            Receive<ConnectionActor.TakeSnapMessage>(msg =>
            {
                Log.Information("Received TakeSnapMessage.");

                _processor.Tell(new ImageProcessingPipelineActor.Start
                {
                    Snap = new ImageProcessingPipelineActor.SnapRecord
                    {
                        OrgId = _orgId,
                        InstanceId = _instanceId
                    }
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
    }
}