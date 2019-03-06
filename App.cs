using System;
using System.Linq;
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

            result.WithParsed(Start);
        }

        /// <summary>
        /// Called with options ot start.
        /// </summary>
        /// <param name="options">The config options.</param>
        static void Start(ConfigurationOptions options)
        {
            // setup logging
            var log = new LoggerConfiguration()
                .WriteTo.ColoredConsole().MinimumLevel.Information()
                .WriteTo
                    .Loggly(
                        customerToken: options.LogglyToken,
                        tags: $"snap-controller,{Environment.GetEnvironmentVariable("ENV_NAME")}")
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
                Log.Information("Arguments : {0}", options);

                var dims = options.Dimensions.ToArray();

                var app = system.ActorOf(
                Props.Create(() => new ApplicationActor(
                    options.Url,
                    options.OrgId,
                    options.Token,
                    dims[0],
                    dims[1],
                    dims[2],
                    dims[3])),
                "app");

                system.WhenTerminated.Wait();
            }
        }
    }
}