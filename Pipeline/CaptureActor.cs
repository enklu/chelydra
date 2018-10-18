using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Akka.Actor;
using Serilog;

namespace CreateAR.Snap
{
    /// <summary>
    /// Actor that tells the camera to take a picture and writes it to disk.
    /// </summary>
    public class CaptureActor : ReceiveActor
    {
        /// <summary>
        /// Message used internally when capture is complete.
        /// </summary>
        private class Complete
        {
            /// <summary>
            /// Unique id.
            /// </summary>
            public string Id;
        }

        /// <summary>
        /// Base directory in which to store images.
        /// </summary>
        private const string BASE_DIR = "snaps";

        /// <summary>
        /// The actor listening for updates.
        /// </summary>
        private readonly IActorRef _listener;

        /// <summary>
        /// Watches the filesystem for changes.
        /// </summary>
        private readonly FileSystemWatcher _watcher;

        /// <summary>
        /// Tracks unique id of image to snap record.
        /// </summary>
        private readonly Dictionary<string, ImageProcessingPipelineActor.SnapRecord> _records = new Dictionary<string, ImageProcessingPipelineActor.SnapRecord>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public CaptureActor(IActorRef listener)
        {
            _listener = listener;

            if (!Directory.Exists(BASE_DIR))
            {
                Directory.CreateDirectory(BASE_DIR);
            }

            _watcher = new FileSystemWatcher(BASE_DIR);
            _watcher.Filter = "*.*";
            _watcher.NotifyFilter = NotifyFilters.LastWrite;
            _watcher.Changed += Watcher_OnCreated(Self);
            _watcher.EnableRaisingEvents = true;

            Receive<ImageProcessingPipelineActor.Start>(msg =>
            {
                Log.Information("Starting capture.");

                var id = Guid.NewGuid().ToString();
                _records[id] = msg.Snap;

                // start capture
                var process = new Process();
                process.StartInfo.FileName = "captura-cli";
                process.StartInfo.Arguments = $"shot --source screen:0 --file {BASE_DIR}/{id}.jpg";
                process.Start();
                process.WaitForExit();
            });

            Receive<Complete>(msg =>
            {
                if (!_records.TryGetValue(msg.Id, out var record))
                {
                    return;
                }

                Log.Information("Capture complete.");

                _records.Remove(msg.Id);

                _listener.Tell(new ImageProcessingPipelineActor.Complete
                {
                    Snap = new ImageProcessingPipelineActor.SnapRecord(record)
                    {
                        SrcPath = $"{BASE_DIR}/{msg.Id}.jpg"
                    }
                });
            });
        }

        /// <summary>
        /// Called when the watcher gets an update. This will happen
        /// asynchronously, so we must tell ourselves.
        /// </summary>
        /// <param name="self">A reference to self.</param>
        private FileSystemEventHandler Watcher_OnCreated(IActorRef self)
        {
            // return a closure with a reference to Self
            return (object sender, FileSystemEventArgs evt) =>
            {
                var id = Path.GetFileNameWithoutExtension(evt.FullPath);

                self.Tell(new Complete
                {
                    Id = id
                });
            };
        }
    }
}
