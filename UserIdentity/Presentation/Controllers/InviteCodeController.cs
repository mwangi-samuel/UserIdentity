using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Resolvers;
using PolyzenKit.Common.Utilities;
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


  [Authorize(Policy = ZenConstants.ADMIN_USER_POLICY)]
  [HttpGet]
  [Route("{id}")]
  public async Task<ActionResult<InviteCodeViewModel>> GetInviteCode(long id)
  {
    var vm = await _getItemQueryHandler.GetItemAsync(new GetInviteCodeQuery { InviteCodeId = id }, RequestAborted);
    return ResolveGetItemActionResult(vm, true);
  }

  [Authorize(Policy = ZenConstants.ADMIN_USER_POLICY)]
  [HttpGet]
  [Route("email/{email}")]
  public async Task<ActionResult<InviteCodeViewModel>> GetInviteCodeByEmail(string email)
  {
    var vm = await _getItemQueryHandler.GetItemAsync(new GetInviteCodeQuery { UserEmail = email }, RequestAborted);
    return ResolveGetItemActionResult(vm, true);
  }

  [Authorize(Policy = ZenConstants.ADMIN_USER_POLICY)]
  [HttpPost]
  [Route("")]
  public async Task<ActionResult<InviteCodeViewModel>> CreateInviteCode(CreateInviteCodeCommand command)
  {
    var registeredApp = await GetRegisteredAppAsync(_registeredAppResolver);

    command.AppId = registeredApp.Id;

    var vm = await _createItemCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);
    return ResolveCreateItemActionResult(vm, true);
  }

}
