using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Resolvers;
using PolyzenKit.Common.Paging;
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
  IGetItemsQueryHandler<GetInviteCodesQuery, InviteCodesViewModel> getItemsQueryHandler,
  IDeleteItemCommandHandler<DeleteInviteCodeCommand, DeleteRecordViewModel> deleteItemCommandHandler,

  IRegisteredAppResolver registeredAppResolver
  ) : BaseController
{

  private readonly ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel> _createItemCommandHandler = createItemCommandHandler;
  private readonly IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel> _getItemQueryHandler = getItemQueryHandler;
  private readonly IGetItemsQueryHandler<GetInviteCodesQuery, InviteCodesViewModel> _getItemsQueryHandler = getItemsQueryHandler;
  private readonly IDeleteItemCommandHandler<DeleteInviteCodeCommand, DeleteRecordViewModel> _deleteItemCommandHandler = deleteItemCommandHandler;

  private readonly IRegisteredAppResolver _registeredAppResolver = registeredAppResolver;

  // App admins work on their token's app, and global admins name the app in the X-App-Name header
  private Task<RegisteredAppEntity> GetAdminAppAsync()
  {
    return IsGlobalAdmin ? GetRegisteredAppAsync(_registeredAppResolver) : _registeredAppResolver.GetActiveAppAsync(AuthorizedParty, RequestAborted);
  }

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpGet]
  [Route("")]
  public async Task<ActionResult<InviteCodesViewModel>> GetInviteCodes(
    [FromQuery, Range(1, Paging.MaxPage)] int page = 1,
    [FromQuery, Range(1, Paging.MaxPageSize)] int pageSize = Paging.DefaultPageSize,
    [FromQuery, MaxLength(100)] string? search = null,
    [FromQuery] bool? applied = null)
  {
    var vm = await _getItemsQueryHandler.GetItemsAsync(new GetInviteCodesQuery { AppId = (await GetAdminAppAsync()).Id, Page = page, PageSize = pageSize, Search = search, Applied = applied }, RequestAborted);
    return ResolveGetItemsActionResult(vm);
  }

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
    command.AppName = registeredApp.AppName;

    var vm = await _createItemCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);
    return ResolveCreateItemActionResult(vm, true);
  }

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpDelete]
  [Route("{id}")]
  public async Task<ActionResult<DeleteRecordViewModel>> DeleteInviteCode(long id)
  {
    var vm = await _deleteItemCommandHandler.DeleteItemAsync(new DeleteInviteCodeCommand { InviteCodeId = id, AppId = (await GetAdminAppAsync()).Id }, LoggedInUserId, RequestAborted);
    return ResolveDeleteItemActionResult(vm);
  }
}
