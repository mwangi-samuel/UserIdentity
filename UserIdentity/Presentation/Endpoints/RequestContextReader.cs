using UserIdentity.Application.Core.Users.Shared;

namespace UserIdentity.Presentation.Endpoints;

public static class RequestContextReader
{
    public static RequestContext Read(HttpContext httpContext)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress;

        if (ipAddress is { IsIPv4MappedToIPv6: true })
            ipAddress = ipAddress.MapToIPv4();

        return RequestContext.Create(ipAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString());
    }
}
