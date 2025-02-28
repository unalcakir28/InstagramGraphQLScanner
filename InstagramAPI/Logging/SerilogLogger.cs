using System;
using Serilog;
using Serilog.Core;

namespace InstagramAPI.Logging
{
    public class SerilogLogger : ILogger
    {
        private readonly Serilog.ILogger _logger;

        public SerilogLogger(Serilog.ILogger logger = null)
        {
            _logger = logger ?? Serilog.Log.Logger;
        }

        public void LogInfo(string message)
        {
            _logger.Information(message);
        }

        public void LogWarning(string message)
        {
            _logger.Warning(message);
        }

        public void LogError(string message, Exception exception = null)
        {
            if (exception != null)
                _logger.Error(exception, message);
            else
                _logger.Error(message);
        }

        public void LogDebug(string message)
        {
            _logger.Debug(message);
        }
    }
}