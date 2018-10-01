using Akka.Actor;
using Serilog;

namespace CreateAR.Snap
{
    /// <summary>
    /// Manages all actors for capturing, processing, and uploading images.
    /// </summary>
    public class ImageProcessingPipelineActor : ReceiveActor
    {
        /// <summary>
        /// Record of a snap that is passed about child actors.
        /// </summary>
        public struct SnapRecord
        {
            /// <summary>
            /// Organization id.
            /// </summary>
            public string OrgId;

            /// <summary>
            /// The instance id.
            /// </summary>
            public string InstanceId;

            /// <summary>
            /// The path to the image src.
            /// </summary>
            public string SrcPath;

            /// <summary>
            /// Copy constructor.
            /// </summary>
            /// <param name="copy">The copy.</param>
            public SnapRecord(SnapRecord copy)
            {
                OrgId = copy.OrgId;
                InstanceId = copy.InstanceId;
                SrcPath = copy.SrcPath;
            }
        }

        /// <summary>
        /// Message passed to child actor to continue processing.
        /// </summary>
        public class Start
        {
            public SnapRecord Snap;
        }

        /// <summary>
        /// Received when a child actor has completed an action.
        /// </summary>
        public class Complete
        {
            public SnapRecord Snap;
        }

        /// <summary>
        /// The capture actor.
        /// </summary>
        private readonly IActorRef _captureRef;

        /// <summary>
        /// The composition actor.
        /// </summary>
        private readonly IActorRef _composeRef;
        
        /// <summary>
        /// The POST actor.
        /// </summary>
        private readonly IActorRef _postRef;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ImageProcessingPipelineActor(string baseUrl, string token)
        {
            _captureRef = Context.ActorOf(Props.Create(() => new CaptureActor(Self)));
            _composeRef = Context.ActorOf(Props.Create(() => new ComposeActor(Self)));
            _postRef = Context.ActorOf(Props.Create(() => new PostActor(
                baseUrl,
                token,
                Self)));

            Receive<Start>(msg =>
            {
                Log.Information("Starting pipeline.", msg.Snap);

                _captureRef.Tell(new Start
                {
                    Snap = msg.Snap
                });
            });

            Receive<Complete>(msg => {

                if (Sender == _captureRef)
                {
                    Log.Information("Moving along to compose.", msg.Snap);

                    _composeRef.Tell(new Start
                    {
                        Snap = msg.Snap
                    });
                }
                else if (Sender == _composeRef)
                {
                    Log.Information("Moving along to post.", msg.Snap);

                    _postRef.Tell(new Start
                    {
                        Snap = msg.Snap
                    });
                }
                else if (Sender == _postRef)
                {
                    Log.Information("Pipeline complete!", msg.Snap);
                }
            });
        }
    }
}