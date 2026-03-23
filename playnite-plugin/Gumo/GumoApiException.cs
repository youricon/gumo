using System;
using System.Net;

namespace Gumo.Playnite
{
    public sealed class GumoApiException : Exception
    {
        public GumoApiException(HttpStatusCode statusCode, string errorCode, string apiMessage, string message)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            ApiMessage = apiMessage;
        }

        public HttpStatusCode StatusCode { get; }

        public string ErrorCode { get; }

        public string ApiMessage { get; }
    }
}
