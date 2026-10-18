using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Resolvers;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Controllers;

using UserIdentity.Application.Core.WaitLists.Commands;
using UserIdentity.Application.Core.WaitLists.Queries;
using UserIdentity.Application.Core.WaitLists.ViewModels;
using UserIdentity.Presentation.Settings;

namespace UserIdentity.Presentation.Controllers;

public class WaitListController(
   ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel> createItemCommandHandler,
   IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel> getItemQueryHandler,

   IRegisteredAppResolver registeredAppResolver
  ) : BaseController
{

  private readonly ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel> _createItemCommandHandler = createItemCommandHandler;
  private readonly IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel> _getItemQueryHandler = getItemQueryHandler;

  private readonly IRegisteredAppResolver _registeredAppResolver = registeredAppResolver;

  [Authorize(Policy = ZenConstants.ADMIN_USER_POLICY)]
  [HttpGet]
  [Route("{id}")]
  public async Task<ActionResult<WaitListViewModel>> GetWaitList(long id)
  {
    var vm = await _getItemQueryHandler.GetItemAsync(new GetWaitListQuery { WaitListId = id }, RequestAborted);
    return ResolveGetItemActionResult(vm, true);
  }

  [Authorize(Policy = ZenConstants.ADMIN_USER_POLICY)]
  [HttpGet]
  [Route("email/{email}")]
  public async Task<ActionResult<WaitListViewModel>> GetWaitListByEmail(string email)
  {
    var vm = await _getItemQueryHandler.GetItemAsync(new GetWaitListQuery { UserEmail = email }, RequestAborted);
    return ResolveGetItemActionResult(vm, true);
  }

  [AllowAnonymous]
  [EnableRateLimiting(RateLimitSettings.ClientIpPolicy)]
  [HttpPost]
  [Route("")]
  public async Task<ActionResult<WaitListViewModel>> CreateWaitList(CreateWaitListCommand command)
  {
    var registeredApp = await GetRegisteredAppAsync(_registeredAppResolver);

    command.AppId = registeredApp.Id;

    var vm = await _createItemCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);
    return ResolveCreateItemActionResult(vm, true);
  }

}
