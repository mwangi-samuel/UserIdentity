using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.WaitLists.Commands;
using UserIdentity.Application.Core.WaitLists.Queries;
using UserIdentity.Application.Core.WaitLists.ViewModels;

namespace UserIdentity.Presentation.Controllers;

public class WaitListController(
   ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel> createItemCommandHandler,
   IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel> getItemQueryHandler,

   IRegisteredAppResolver registeredAppResolver
  ) : AppAdminController(registeredAppResolver)
{

  private readonly ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel> _createItemCommandHandler = createItemCommandHandler;
  private readonly IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel> _getItemQueryHandler = getItemQueryHandler;

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpGet]
  [Route("{id}")]
  public async Task<ActionResult<WaitListViewModel>> GetWaitList(long id)
  {
    var vm = await _getItemQueryHandler.GetItemAsync(new GetWaitListQuery { WaitListId = id, AppId = (await GetAdminAppAsync()).Id }, RequestAborted);
    return ResolveGetItemActionResult(vm, true);
  }

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpGet]
  [Route("email/{email}")]
  public async Task<ActionResult<WaitListViewModel>> GetWaitListByEmail(string email)
  {
    var vm = await _getItemQueryHandler.GetItemAsync(new GetWaitListQuery { UserEmail = email, AppId = (await GetAdminAppAsync()).Id }, RequestAborted);
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
