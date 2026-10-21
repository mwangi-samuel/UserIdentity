using System.Collections.Generic;
using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.JsonWebTokens;

using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Presentation.Controllers;

namespace UserIdentity.UnitTests.Presentation.Controllers;

internal static class ControllerUtils
{

  public static void UpdateContext(
      this BaseController controller,
      string? controllerName,
      bool addUserId = false,
      string? userId = null,
      bool addUserRoles = false,
      string? userRoles = null,
      bool addUserScopes = false,
      string? userScopes = null,
      string? appName = null,
      string? authorizedParty = "testapp"
   )
  {
    RouteData routedData = new();
    routedData.Values["controller"] = controllerName;

    controller.ControllerContext.RouteData = routedData;

    DefaultHttpContext context = new();

    List<Claim> claims = [];

    if (addUserId)
      claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId!));

    if (authorizedParty is not null)
      claims.Add(new Claim(JwtRegisteredClaimNames.Azp, authorizedParty));

    if (addUserRoles)
      foreach (var role in userRoles!.Split(","))
        claims.Add(new Claim(ClaimTypes.Role, role));

    if (addUserScopes)
      foreach (var scope in userScopes!.Split(","))
        claims.Add(new Claim(JwtCustomClaimNames.Scope, scope));

    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

    if (!string.IsNullOrWhiteSpace(appName))
      context.Request.Headers["X-App-Name"] = appName;

    controller.ControllerContext.HttpContext = context;

  }
}
