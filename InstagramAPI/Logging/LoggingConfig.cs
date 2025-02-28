using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using System.IO;

namespace InstagramAPI.Logging
{
    public static class LoggingConfig
    {
        public static void ConfigureLogging(string configPath = null)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(configPath ?? Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
        }

        public static ILogger CreateLogger()
        {
            return new SerilogLogger();
        }
    }
}