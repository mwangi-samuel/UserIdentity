using PolyzenKit.Presentation.Extensions;
using UserIdentity.Application.Core.Users.Shared;

namespace UserIdentity.Presentation.Endpoints;

public static class RequestContextReader
{
    public static RequestContext Read(HttpContext httpContext) =>
        RequestContext.Create(httpContext.GetClientAddress(), httpContext.Request.Headers.UserAgent.ToString());
}
