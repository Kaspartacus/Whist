using System.Net;

namespace WebApp.Service.ApiErrors;

public sealed class ApiRequestException : Exception
{
    public ApiRequestException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
