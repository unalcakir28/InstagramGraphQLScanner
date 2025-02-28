using System;
using System.Net;

namespace InstagramAPI.Exceptions
{
    public class InstagramApiException : Exception
    {
        public HttpStatusCode? StatusCode { get; }
        public string Endpoint { get; }

        public InstagramApiException(string message) : base(message)
        {
        }

        public InstagramApiException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        public InstagramApiException(string message, string endpoint, HttpStatusCode statusCode) 
            : base(message)
        {
            Endpoint = endpoint;
            StatusCode = statusCode;
        }
    }

    public class InstagramAuthException : InstagramApiException 
    {
        public InstagramAuthException(string message) : base(message)
        {
        }
    }

    public class InstagramRateLimitException : InstagramApiException 
    {
        public DateTime? RetryAfter { get; }

        public InstagramRateLimitException(string message, DateTime? retryAfter = null) 
            : base(message)
        {
            RetryAfter = retryAfter;
        }
    }
}