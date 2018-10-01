using System;
using Akka.Actor;
using Akka.Configuration;
using Serilog;

namespace CreateAR.Snap
{
    /// <summary>
    /// Entry point of application.
    /// </summary>
    class App
    {
        /// <summary>
        /// Entry.
        /// </summary>
        /// <param name="args">Command line parameters.</param>
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
                    Props.Create(() => new ApplicationActor(
                        "http://localhost:9999",
                        "744d26da-959d-48ce-93b7-ec1071b39e24",
                        "instance",
                        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3N1ZXIiOiJ0cmVsbGlzIiwiYXVkaWVuY2UiOiJ0cmVsbGlzIiwic3ViamVjdCI6IjczYWNjYTVhLTMxZTUtNGQwOC1iOTIyLWJjMzZlYzFiZmU1MSIsImV2ZW50UXVldWUiOiJRbVZ1YW1GdGFXNXpMVTFoWTBKdmIyc3RVSEp2TG14dlkyRnNfQXNzZXRzIiwiaWF0IjoxNTA3NzY0OTY0LCJleHAiOjE1MzkzMDA5NjR9.i7l6SWezgbnG6gS12lsiduUI391erfGPmT88Ry8ua9s"
                    )),
                    "app");

                system.WhenTerminated.Wait();
            }
        }
    }
}
