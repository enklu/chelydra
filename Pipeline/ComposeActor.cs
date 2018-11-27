using System;
using System.IO;
using System.Linq;
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
        /// PRNG for choosing an overlay.
        /// </summary>
        /// <returns></returns>
        private static readonly Random RANDOM = new Random();

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
        private readonly Dictionary<string, List<Image<Rgba32>>> _overlays = new Dictionary<string, List<Image<Rgba32>>>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public ComposeActor(IActorRef listener)
        {
            _listener = listener;

            // watch
            var watcher = new FileSystemWatcher(OVERLAY_BASE);
            watcher.IncludeSubdirectories = true;
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
            if (!_overlays.TryGetValue(instanceId, out var overlays))
            {
                overlays = _overlays[instanceId] = LoadOverlays(instanceId);
            }

            return ChooseOverlay(overlays);
        }

        /// <summary>
        /// Loads all overlays for an instance id.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns></returns>
        private List<Image<Rgba32>> LoadOverlays(string instanceId)
        {
            var path = Path.Combine(OVERLAY_BASE, instanceId);
            if (Directory.Exists(path))
            {
                var images = Directory
                    .GetFiles(path)
                    .Select(f => {
                        try
                        {
                            return Image.Load(f);
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(i => null != i)
                    .ToList();

                Log.Information($"Loaded {images.Count} overlays for instance '{instanceId}'.");

                return images;
            }

            return new List<Image<Rgba32>>();
        }

        /// <summary>
        /// Returns a random overlay from a list of overlays.
        /// </summary>
        /// <param name="overlays">The overlays to choose from.</param>
        /// <returns></returns>
        private Image<Rgba32> ChooseOverlay(List<Image<Rgba32>> overlays)
        {
            var len = overlays.Count;
            if (0 == len)
            {
                return null;
            }

            return overlays[(int) Math.Floor(RANDOM.NextDouble() * len)];
        }

        /// <summary>
        /// Called when there's a filesystem change to the overlay directory.
        /// </summary>
        private void FileSystem_OnChanged(
            object sender,
            FileSystemEventArgs evt)
        {
            Log.Information("Overlays updated. Releasing all overlays.");

            // kill all the cached overlays
            foreach (var overlays in _overlays.Values)
            {
                overlays.ForEach(o => o.Dispose());
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