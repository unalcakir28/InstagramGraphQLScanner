using System;

namespace InstagramAPI.Configuration
{
    public class InstagramApiConfig
    {
        public string BaseUrl { get; set; } = "https://www.instagram.com";
        public string ApiVersion { get; set; } = "api/v1";
        public string AppId { get; set; } = "936619743392459";
        public string UserAgent { get; set; } = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36";
        
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 1000;
        public int MinRequestDelayMs { get; set; } = 2000;
        public int MaxRequestDelayMs { get; set; } = 4000;
        
        public string LogPrefix { get; set; } = "[InstagramAPI]";
        public bool IncludeTimestampInLogs { get; set; } = true;
        
        public static InstagramApiConfig Default => new InstagramApiConfig();
    }
}