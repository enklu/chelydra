using System;
using Akka.Actor;
using Akka.Configuration;
using CommandLine;
using CommandLine.Text;
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
            var result = Parser.Default.ParseArguments<ConfigurationOptions>(args);
            if (result.Tag == ParserResultType.NotParsed)
            {
                Console.Write(HelpText.AutoBuild(result));
                return;
            }

            // setup logging
            var log = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .MinimumLevel.Information()
                .CreateLogger();
            Log.Logger = log;
            Log.Information("Logging initialized.");

            // setup Akka
            var config = ConfigurationFactory.ParseString(@"
akka {
    loglevl = INFO
    loggers = [""Akka.Logger.Serilog.SerilogLogger, Akka.Logger.Serilog""]   
}");

            // setup actor system
            using (var system = ActorSystem.Create("snap-controller", config))
            {
                result.WithParsed(pargs =>
                {
                    Log.Information("Arguments : {0}", pargs);

                    var app = system.ActorOf(
                    Props.Create(() => new ApplicationActor(
                        pargs.Url,
                        pargs.OrgId,
                        pargs.Token)),
                    "app");
                });

                system.WhenTerminated.Wait();
            }
        }
    }
}