using System;
using Akka.Actor;
using Serilog;

namespace CreateAR.Snap
{
    public class ComposeActor : ReceiveActor
    {
        private readonly IActorRef _listener;

        public ComposeActor(IActorRef listener)
        {
            _listener = listener;

            Receive<ImageProcessingPipelineActor.Compose>(msg =>
            {
                Log.Information("Starting compose.");

                // TODO: ... compose

                // STUB
                Context.System.Scheduler.ScheduleTellOnce(
                    TimeSpan.FromSeconds(1),
                    _listener,
                    new ImageProcessingPipelineActor.ComposeComplete
                    {
                        Snap = msg.Snap
                    },
                    null);
            });
        }
    }
}
