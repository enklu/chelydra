using System.IO;
using Akka.Actor;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Advanced;
using SixLabors.Primitives;

namespace CreateAR.Snap
{
    /// <summary>
    /// Creates a thumbnail.
    /// </summary>
    public class ThumbActor : ReceiveActor
    {
        /// <summary>
        /// Listens for complete.
        /// </summary>
        private readonly IActorRef _listener;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ThumbActor(IActorRef listener)
        {
            _listener = listener;

            Receive<ImageProcessingPipelineActor.Start>(msg => Process(msg.Snap));
        }

        /// <summary>
        /// Creates the thumbnail.
        /// </summary>
        /// <param name="snap">The snap.</param>
        private void Process(ImageProcessingPipelineActor.SnapRecord snap)
        {
            var thumbPath = ThumbPath(snap.SrcPath);

            // make a thumb
            using (var image = Image.Load<Rgba32>(snap.SrcPath))
            {
                image.Mutate(ctx => 
                {
                    ctx.Resize(image.Width / 4, image.Height / 4);
                });

                using (var stream = File.Open(
                    thumbPath,
                    FileMode.CreateNew))
                {
                    image.SaveAsJpeg(stream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
                    {
                        Quality = 80
                    });
                }
            }

            // report complete!
            _listener.Tell(new ImageProcessingPipelineActor.Complete
            {
                Snap = new ImageProcessingPipelineActor.SnapRecord(snap)
                {
                    ThumbSrcPath = thumbPath
                }
            });
        }

        /// <summary>
        /// Generates a path for the thumb.
        /// </summary>
        /// <param name="path">The original path.</param>
        private static string ThumbPath(string path)
        {
            return Path.Combine(
                Path.GetDirectoryName(path),
                $"{Path.GetFileNameWithoutExtension(path)}.thumb.jpg");
        }
    }
}