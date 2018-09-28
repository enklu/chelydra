using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Akka.Actor;
using Serilog;

namespace CreateAR.Snap
{
    public class CaptureActor : ReceiveActor
    {
        private class Complete
        {
            public string Id;
        }

        private const string BASE_DIR = "snaps";

        private readonly IActorRef _listener;

        private readonly FileSystemWatcher _watcher;

        private readonly Dictionary<string, ImageProcessingPipelineActor.SnapRecord> _records = new Dictionary<string, ImageProcessingPipelineActor.SnapRecord>();

        public CaptureActor(IActorRef listener)
        {
            _listener = listener;

            if (!Directory.Exists(BASE_DIR))
            {
                Directory.CreateDirectory(BASE_DIR);
            }

            _watcher = new FileSystemWatcher(BASE_DIR);
            _watcher.Filter = "*.jpg";
            _watcher.NotifyFilter = NotifyFilters.LastWrite;
            _watcher.Created += Watcher_OnCreated(Self);
            _watcher.EnableRaisingEvents = true;

            Receive<ImageProcessingPipelineActor.Capture>(msg =>
            {
                Log.Information("Starting capture.");

                var id = Guid.NewGuid().ToString();
                _records[id] = msg.Snap;

                // start capture
                var process = new Process();
                process.StartInfo.FileName = "gphoto2";
                process.StartInfo.Arguments = $"--capture-image-and-download --force-overwrite --filename={id}.jpg";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
            });

            Receive<Complete>(msg =>
            {
                Log.Information("Capture complete.");

                var record = _records[msg.Id];
                _records.Remove(msg.Id);

                _listener.Tell(new ImageProcessingPipelineActor.CaptureComplete
                {
                    Snap = new ImageProcessingPipelineActor.SnapRecord(record)
                    {
                        SrcPath = $"{BASE_DIR}/{msg.Id}.jpg"
                    }
                });
            });
        }

        private FileSystemEventHandler Watcher_OnCreated(IActorRef self)
        {
            return (object sender, FileSystemEventArgs evt) =>
            {
                Log.Information("Snapshot exported from device.");

                var id = Path.GetFileNameWithoutExtension(evt.FullPath);

                self.Tell(new Complete
                {
                    Id = id
                });
            };
        }
    }
}
