using System.Net;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Extensions;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Controllers;

using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;

namespace UserIdentity.Presentation.Controllers;

[Authorize]
public class RoleController(
    ICreateItemCommandHandler<CreateRoleCommand, RoleViewModel> createRoleCommandHandler,
    ICreateItemCommandHandler<CreateUserRoleCommand, UserRolesViewModel> createUserRoleCommandHandler,
    IGetItemQueryHandler<GetRoleQuery, RoleViewModel> getRoleQueryHandler,
    IGetItemsQueryHandler<GetRolesQuery, RolesViewModel> getRolesQueryHandler,
    IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel> getUserRolesQueryHandler,
    IUpdateItemCommandHandler<UpdateRoleCommand, RoleViewModel> updateRoleCommandHandler,
    IDeleteItemCommandHandler<DeleteRoleCommand, DeleteRecordViewModel> deleteRoleCommandHandler,
    ICreateItemCommandHandler<CreateRoleClaimCommand, RoleClaimViewModel> createRoleClaimCommandHandler,
    IGetItemsQueryHandler<GetRoleClaimsQuery, RoleClaimsViewModel> getRoleClaimsQueryHandler,
    IDeleteItemCommandHandler<DeleteRoleClaimCommand, DeleteRecordViewModel> deleteRoleClaimCommandHandler
    ) : BaseController
{
  private readonly ICreateItemCommandHandler<CreateRoleCommand, RoleViewModel> _createRoleCommandHandler = createRoleCommandHandler;
  private readonly ICreateItemCommandHandler<CreateUserRoleCommand, UserRolesViewModel> _createUserRoleCommandHandler = createUserRoleCommandHandler;
  private readonly IGetItemQueryHandler<GetRoleQuery, RoleViewModel> _getRoleQueryHandler = getRoleQueryHandler;
  private readonly IGetItemsQueryHandler<GetRolesQuery, RolesViewModel> _getRolesQueryHandler = getRolesQueryHandler;
  private readonly IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel> _getUserRolesQueryHandler = getUserRolesQueryHandler;
  private readonly IUpdateItemCommandHandler<UpdateRoleCommand, RoleViewModel> _updateRoleCommandHandler = updateRoleCommandHandler;
  private readonly IDeleteItemCommandHandler<DeleteRoleCommand, DeleteRecordViewModel> _deleteRoleCommandHandler = deleteRoleCommandHandler;
  private readonly ICreateItemCommandHandler<CreateRoleClaimCommand, RoleClaimViewModel> _createRoleClaimCommandHandler = createRoleClaimCommandHandler;
  private readonly IGetItemsQueryHandler<GetRoleClaimsQuery, RoleClaimsViewModel> _getRoleClaimsQueryHandler = getRoleClaimsQueryHandler;
  private readonly IDeleteItemCommandHandler<DeleteRoleClaimCommand, DeleteRecordViewModel> _deleteRoleClaimCommandHandler = deleteRoleClaimCommandHandler;

  private string? AdminAppName => IsGlobalAdmin ? null : AuthorizedParty;

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpGet]
  public async Task<ActionResult<RolesViewModel>> GetRolesAsync()
  {
    var vm = await _getRolesQueryHandler.GetItemsAsync(new GetRolesQuery { AppName = AdminAppName }, RequestAborted);
    return ResolveGetItemsActionResult(vm);
  }

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpGet]
  [Route("{roleId}")]
  public async Task<ActionResult<RoleViewModel>> GetRoleAsync(string roleId)
  {
    var vm = await _getRoleQueryHandler.GetItemAsync(new GetRoleQuery { RoleId = roleId, AppName = AdminAppName }, RequestAborted);
    return ResolveGetItemActionResult(vm);
  }

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpPost]
  public async Task<ActionResult<RoleViewModel>> CreateRoleAsync(CreateRoleCommand command)
  {
    command.AppName = AdminAppName;

    var vm = await _createRoleCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);
    return ResolveCreateItemActionResult(vm);
  }

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpPut]
  [Route("{roleId}")]
  public async Task<ActionResult<RoleViewModel>> UpdateRoleAsync(string roleId, [FromBody] UpdateRoleCommand command)
  {
    command.RoleId = roleId;
    command.AppName = AdminAppName;

    var vm = await _updateRoleCommandHandler.UpdateItemAsync(command, LoggedInUserId, RequestAborted);
    return ResolveUpdateItemActionResult(vm);
  }


  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpDelete]
  [Route("{roleId}")]
  public async Task<ActionResult<DeleteRecordViewModel>> DeleteRoleAsync(string roleId)
  {
    var vm = await _deleteRoleCommandHandler.DeleteItemAsync(new DeleteRoleCommand { RoleId = roleId, AppName = AdminAppName }, LoggedInUserId, RequestAborted);
    return ResolveDeleteItemActionResult(vm);
  }

  [Authorize(Policy = ZenConstants.ADMIN_GLOBAL_ADMIN_OR_SAME_USER_POLICY)]
  [HttpGet]
  [Route("user/{userId}")]
  public async Task<ActionResult<UserRolesViewModel>> GetUserRolesAsync(string userId)
  {
    var vm = await _getUserRolesQueryHandler.GetItemsAsync(new GetUserRolesQuery { UserId = userId, AppName = AdminAppName }, RequestAborted);
    return ResolveGetItemsActionResult(vm);
  }


  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpPost]
  [Route("user")]
  public async Task<ActionResult<UserRolesViewModel>> CreateUserRoleAsync(CreateUserRoleCommand command)
  {
    command.AppName = AdminAppName;

    var vm = await _createUserRoleCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);

    vm.ResolveCreateDownloadRights(UserScopeClaims, ScopeResource, IsAdmin || IsGlobalAdmin);
    vm.ResolveRequestStatus(RequestStatus.SUCCESSFUL, ItemStatusMessage.CREATE_ITEM_SUCCESSFUL);

    return StatusCode((int)HttpStatusCode.Created, vm);
  }


  /// <summary>
  /// Roles Claims
  /// </summary>
  /// <param name="command"></param>
  /// <returns></returns>

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpPost]
  [Route("claim")]
  public async Task<ActionResult<RoleClaimViewModel>> CreateRoleClaimAsync(CreateRoleClaimCommand command)
  {
    command.AppName = AdminAppName;

    var vm = await _createRoleClaimCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);
    return ResolveCreateItemActionResult(vm);
  }

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpGet]
  [Route("claim/{roleId}")]
  public async Task<ActionResult<RoleClaimsViewModel>> GetRoleClaimsAsync(string roleId)
  {
    var vm = await _getRoleClaimsQueryHandler.GetItemsAsync(new GetRoleClaimsQuery { RoleId = roleId, AppName = AdminAppName }, RequestAborted);
    return ResolveGetItemsActionResult(vm);
  }

  [Authorize(Policy = ZenConstants.ADMIN_OR_GLOBAL_ADMIN_POLICY)]
  [HttpDelete]
  [Route("claim")]
  public async Task<ActionResult<DeleteRecordViewModel>> DeleteRoleClaimAsync(DeleteRoleClaimCommand command)
  {
    command.AppName = AdminAppName;

    var vm = await _deleteRoleClaimCommandHandler.DeleteItemAsync(command, LoggedInUserId, RequestAborted);
    return ResolveDeleteItemActionResult(vm);
  }
}
