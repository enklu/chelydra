using System;
using System.IO;
using Akka.Actor;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Advanced;

namespace CreateAR.Snap
{
    public class ComposeActor : ReceiveActor
    {
        private readonly IActorRef _listener;

        private Image<Rgba32> _overlay;

        public ComposeActor(IActorRef listener)
        {
            _listener = listener;

            // load overlay
            _overlay = Image.Load("./overlays/overlay-1.png");

            Receive<ImageProcessingPipelineActor.Compose>(msg =>
            {
                Log.Information("Starting compose.");

                // load target
                using (var image = Image.Load(msg.Snap.SrcPath))
                {
                    if (image.Width != _overlay.Width
                        || image.Height != _overlay.Height)
                    {
                        Log.Error($"Invalid image dimensions! Expected ${_overlay.Width}x${_overlay.Height} but got ${image.Width}x${image.Height}.");
                        return;
                    }

                    // apply additive blend
                    for (var y = 0; y < image.Height; y++)
                    {
                        var span = image.GetPixelRowSpan(y);
                        var overlaySpan = _overlay.GetPixelRowSpan(y);

                        for (var x = 0; x < image.Width; x++)
                        {
                            var color = span[x];
                            var overlayColor = overlaySpan[x];
                            span[x] = new Rgba32(
                                color.R + overlayColor.R,
                                color.G + overlayColor.G,
                                color.B + overlayColor.B);
                        }
                    }

                    using (var stream = File.Open(msg.Snap.SrcPath, FileMode.Truncate))
                    {
                        image.SaveAsPng(stream);
                    }
                }

                // complete
                _listener.Tell(new ImageProcessingPipelineActor.ComposeComplete
                {
                    Snap = msg.Snap
                });
            });
        }
    }
}
