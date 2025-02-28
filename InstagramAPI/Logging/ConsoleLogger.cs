using System;

namespace InstagramAPI.Logging
{
    public class ConsoleLogger : ILogger
    {
        private readonly string _prefix;
        private readonly bool _includeTimestamp;

        public ConsoleLogger(string prefix = "[InstagramAPI]", bool includeTimestamp = true)
        {
            _prefix = prefix;
            _includeTimestamp = includeTimestamp;
        }

        private string FormatMessage(string level, string message)
        {
            var timestamp = _includeTimestamp ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " : "";
            return $"{timestamp}{_prefix} [{level}] {message}";
        }

        public void LogInfo(string message)
        {
            Console.WriteLine(FormatMessage("INFO", message));
        }

        public void LogWarning(string message)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(FormatMessage("WARN", message));
            Console.ForegroundColor = originalColor;
        }

        public void LogError(string message, Exception exception = null)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(FormatMessage("ERROR", message));
            if (exception != null)
            {
                Console.WriteLine(FormatMessage("ERROR", $"Exception: {exception.Message}"));
                Console.WriteLine(FormatMessage("ERROR", $"StackTrace: {exception.StackTrace}"));
            }
            Console.ForegroundColor = originalColor;
        }

        public void LogDebug(string message)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(FormatMessage("DEBUG", message));
            Console.ForegroundColor = originalColor;
        }
    }
}