using System;
using Akka.Actor;
using Serilog;

namespace CreateAR.Snap
{
    public class CaptureActor : ReceiveActor
    {
        private readonly IActorRef _listener;

        public CaptureActor(IActorRef listener)
        {
            _listener = listener;

            Receive<ImageProcessingPipelineActor.Capture>(msg =>
            {
                Log.Information("Starting capture.");

                // TODO: ... capture

                // STUB
                Context.System.Scheduler.ScheduleTellOnce(
                    TimeSpan.FromSeconds(1),
                    _listener,
                    new ImageProcessingPipelineActor.CaptureComplete
                    {
                        Snap = msg.Snap
                    },
                    null);
            });
        }
    }
}
