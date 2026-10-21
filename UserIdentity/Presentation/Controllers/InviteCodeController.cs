using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Resolvers;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Presentation.Controllers;

using UserIdentity.Application.Core.InviteCodes.Commands;
using UserIdentity.Application.Core.InviteCodes.Queries;
using UserIdentity.Application.Core.InviteCodes.ViewModels;

namespace UserIdentity.Presentation.Controllers;

[Authorize]
public class InviteCodeController(
  ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel> createItemCommandHandler,
  IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel> getItemQueryHandler,

  IRegisteredAppResolver registeredAppResolver
  ) : BaseController
{

  private readonly ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel> _createItemCommandHandler = createItemCommandHandler;
  private readonly IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel> _getItemQueryHandler = getItemQueryHandler;

  private readonly IRegisteredAppResolver _registeredAppResolver = registeredAppResolver;


  // App admins work on their token's app, and global admins name the app in the X-App-Name header
  private Task<RegisteredAppEntity> GetAdminAppAsync() =>
    IsGlobalAdmin ? GetRegisteredAppAsync(_registeredAppResolver) : _registeredAppResolver.GetActiveAppAsync(AuthorizedParty, RequestAborted);

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpGet]
  [Route("{id}")]
  public async Task<ActionResult<InviteCodeViewModel>> GetInviteCode(long id)
  {
    var vm = await _getItemQueryHandler.GetItemAsync(new GetInviteCodeQuery { InviteCodeId = id, AppId = (await GetAdminAppAsync()).Id }, RequestAborted);
    return ResolveGetItemActionResult(vm, true);
  }

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpGet]
  [Route("email/{email}")]
  public async Task<ActionResult<InviteCodeViewModel>> GetInviteCodeByEmail(string email)
  {
    var vm = await _getItemQueryHandler.GetItemAsync(new GetInviteCodeQuery { UserEmail = email, AppId = (await GetAdminAppAsync()).Id }, RequestAborted);
    return ResolveGetItemActionResult(vm, true);
  }

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpPost]
  [Route("")]
  public async Task<ActionResult<InviteCodeViewModel>> CreateInviteCode(CreateInviteCodeCommand command)
  {
    var registeredApp = await GetAdminAppAsync();

    command.AppId = registeredApp.Id;

    var vm = await _createItemCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);
    return ResolveCreateItemActionResult(vm, true);
  }

}
