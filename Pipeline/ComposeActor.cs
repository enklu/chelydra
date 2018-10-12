using System;
using System.IO;
using Akka.Actor;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Advanced;
using System.Collections.Generic;

namespace CreateAR.Snap
{
    /// <summary>
    /// Actor that composes an overlay with the capture.
    /// </summary>
    public class ComposeActor : ReceiveActor
    {
        /// <summary>
        /// Base directory for overlays.
        /// </summary>
        private const string OVERLAY_BASE = "./overlays";

        /// <summary>
        /// The actor listening for updates.
        /// </summary>
        private readonly IActorRef _listener;

        /// <summary>
        /// Caches the overlays.
        /// </summary>
        private readonly Dictionary<string, Image<Rgba32>> _overlays = new Dictionary<string, Image<Rgba32>>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public ComposeActor(IActorRef listener)
        {
            _listener = listener;

            // watch
            var watcher = new FileSystemWatcher(OVERLAY_BASE);
            watcher.Created += FileSystem_OnChanged;
            watcher.Changed += FileSystem_OnChanged;
            watcher.Deleted += FileSystem_OnChanged;
            watcher.EnableRaisingEvents = true;

            Receive<ImageProcessingPipelineActor.Start>(msg => Process(msg));
        }

        /// <summary>
        /// Processes a snap.
        /// </summary>
        /// <param name="msg">The message received.</param>
        private void Process(ImageProcessingPipelineActor.Start msg)
        {
            Log.Information("Starting compose.");

            // load overlay
            var overlay = GetOverlay(msg.Snap.InstanceId);

            // load target
            using (var image = Image.Load<Rgba32>(msg.Snap.SrcPath))
            {
                if (image.Width != overlay.Width
                    || image.Height != overlay.Height)
                {
                    Log.Error($"Invalid image dimensions! Expected ${overlay.Width}x${overlay.Height} but got ${image.Width}x${image.Height}.");
                    return;
                }

                // apply additive blend
                for (var y = 0; y < image.Height; y++)
                {
                    var span = image.GetPixelRowSpan(y);
                    var overlaySpan = overlay.GetPixelRowSpan(y);

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
        }

        /// <summary>
        /// Retrieves overlay.
        /// </summary>
        /// <param name="instanceId">The id of the overlay.</param>
        /// <returns></returns>
        private Image<Rgba32> GetOverlay(string instanceId)
        {
            if (!_overlays.TryGetValue(instanceId, out var overlay))
            {
                overlay = _overlays[instanceId] = Image.Load<Rgba32>(Path.Combine(
                    OVERLAY_BASE,
                    $"{instanceId}.png"
                ));
            }

            return overlay;
        }

        /// <summary>
        /// Called when there's a filesystem change to the overlay directory.
        /// </summary>
        private void FileSystem_OnChanged(
            object sender,
            FileSystemEventArgs evt)
        {
            Log.Information("Overlays updates.");

            // kill all the cached overlays
            foreach (var image in _overlays.Values)
            {
                image.Dispose();
            }
            _overlays.Clear();
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