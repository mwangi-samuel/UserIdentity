using Microsoft.AspNetCore.Http.HttpResults;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.KeySets.Queries;
using PolyzenKit.Application.Core.KeySets.ViewModels;
using PolyzenKit.Presentation.Endpoints;

namespace UserIdentity.Presentation.Endpoints;

public static class JwksEndpoints
{
  public static RouteGroupBuilder MapJwksEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapApiGroup("Jwks");

    group.MapGet("keys", GetKeySetsAsync).AllowAnonymous();

    return group;
  }

  internal static async Task<JsonHttpResult<IDictionary<string, IList<Dictionary<string, string>>>>> GetKeySetsAsync(
    IGetItemsQueryHandler<GetKeySetsQuery, KeySetsViewModel> handler,
    CancellationToken cancellationToken)
  {
    var keySets = await handler.GetItemsAsync(new GetKeySetsQuery(), cancellationToken);

    return TypedResults.Json(keySets.KeySetList);
  }
}
