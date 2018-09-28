using System;
using Akka.Actor;
using Serilog;

namespace CreateAR.Snap
{
    public class PostActor : ReceiveActor
    {
        private readonly IActorRef _listener;

        public PostActor(IActorRef listener)
        {
            _listener = listener;

            Receive<ImageProcessingPipelineActor.Post>(msg =>
            {
                Log.Information("Starting post.");

                // TODO: ... post

                // STUB
                Context.System.Scheduler.ScheduleTellOnce(
                    TimeSpan.FromSeconds(1),
                    _listener,
                    new ImageProcessingPipelineActor.PostComplete
                    {
                        Snap = msg.Snap
                    },
                    null);
            });
        }
    }
}
