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
    /// <summary>
    /// Actor that composes an overlay with the capture.
    /// </summary>
    public class ComposeActor : ReceiveActor
    {
        /// <summary>
        /// The actor listening for updates.
        /// </summary>
        private readonly IActorRef _listener;

        /// <summary>
        /// The overlay, loaded once.
        /// </summary>
        private Image<Rgba32> _overlay;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ComposeActor(IActorRef listener)
        {
            _listener = listener;

            // load overlay
            _overlay = Image.Load("./overlays/overlay-1.png");

            Receive<ImageProcessingPipelineActor.Start>(msg =>
            {
                Log.Information("Starting compose.");

                // load target
                using (var image = Image.Load<Rgba32>(msg.Snap.SrcPath))
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
                                (color.R + overlayColor.R) / 255f,
                                (color.G + overlayColor.G) / 255f,
                                (color.B + overlayColor.B) / 255f);
                        }
                    }

                    using (var stream = File.Open(
                        ProcessedSnapPath(msg.Snap.SrcPath),
                        FileMode.CreateNew))
                    {
                        image.SaveAsJpeg(stream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder()
                        {
                            Quality = 80
                        });
                    }
                }

                // delete
                File.Delete(msg.Snap.SrcPath);

                // complete
                _listener.Tell(new ImageProcessingPipelineActor.Complete
                {
                    Snap = new ImageProcessingPipelineActor.SnapRecord(msg.Snap)
                    {
                        SrcPath = ProcessedSnapPath(msg.Snap.SrcPath)
                    }
                });
            });
        }

        /// <summary>
        /// Generates a path for the processed image.
        /// </summary>
        /// <param name="path">The original path.</param>
        private static string ProcessedSnapPath(string path)
        {
            return Path.Combine(
                Path.GetDirectoryName(path),
                $"{Path.GetFileNameWithoutExtension(path)}.processed.jpg");
        }
    }
}
