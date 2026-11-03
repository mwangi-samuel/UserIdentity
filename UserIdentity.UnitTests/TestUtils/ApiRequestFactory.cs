using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

using PolyzenKit.Common.Settings;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Endpoints;

namespace UserIdentity.UnitTests.TestUtils;

internal static class ApiRequestFactory
{
  public static ApiRequest Create(
    string entityName,
    string? userId = null,
    string? appName = null,
    string? authorizedParty = "testapp")
  {
    List<Claim> claims = [];

    if (userId is not null)
      claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId));

    if (authorizedParty is not null)
      claims.Add(new Claim(JwtRegisteredClaimNames.Azp, authorizedParty));

    var httpContext = new DefaultHttpContext
    {
      User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
      RequestServices = new ServiceCollection()
        .AddSingleton(Options.Create(new RoleSettings { ServiceName = "useridentity" }))
        .BuildServiceProvider()
    };

    if (appName is not null)
      httpContext.Request.Headers[CustomHeaderNames.AppName] = appName;

    httpContext.SetEndpoint(
      new Endpoint(null, new EndpointMetadataCollection(new ScopeEntityMetadata(entityName)), entityName));

    return ApiRequest.For(httpContext);
  }
}
