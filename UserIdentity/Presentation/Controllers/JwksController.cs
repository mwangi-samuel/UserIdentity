using System.Net;

using Microsoft.AspNetCore.Mvc;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.KeySets.Queries;
using PolyzenKit.Application.Core.KeySets.ViewModels;
using PolyzenKit.Presentation.Controllers;

namespace UserIdentity.Presentation.Controllers;

public class JwksController(
    IGetItemsQueryHandler<GetKeySetsQuery, KeySetsViewModel> getKeySetsQueryHandler
    ) : BaseController
{
  private readonly IGetItemsQueryHandler<GetKeySetsQuery, KeySetsViewModel> _getKeySetsQueryHandler = getKeySetsQueryHandler;

  [HttpGet]
  [Route("keys")]
  public async Task<IActionResult> GetKeySetsAsync()
  {
    var keySets = await _getKeySetsQueryHandler.GetItemsAsync(new GetKeySetsQuery { }, RequestAborted);

    return StatusCode((int)HttpStatusCode.OK, keySets.KeySetList);
  }
}
