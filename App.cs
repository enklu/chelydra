using System;
using Akka.Actor;
using Akka.Configuration;
using Serilog;

namespace CreateAR.Snap
{
    class App
    {
        static void Main(string[] args)
        {
            var log = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .MinimumLevel.Information()
                .CreateLogger();
            Log.Logger = log;

            Log.Information("Logging initialized.");

            var config = ConfigurationFactory.ParseString(@"
akka {
    loglevl = INFO
    loggers = [""Akka.Logger.Serilog.SerilogLogger, Akka.Logger.Serilog""]   
}");

            using (var system = ActorSystem.Create("snap-controller", config))
            {
                var app = system.ActorOf(
                    Props.Create(() => new ApplicationActor()),
                    "app");

                app.Tell(new ApplicationActor.Start());

                system.WhenTerminated.Wait();
            }
        }
    }
}
