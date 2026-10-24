using System.Net;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Extensions;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Resolvers;
using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Dto;
using PolyzenKit.Presentation.Controllers;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Enums;

namespace UserIdentity.Presentation.Controllers;

[Authorize]
public class UserController(
    IGetItemQueryHandler<GetUserQuery, UserViewModel> getUserQueryHandler,
    ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel> registerUserComandHandler,
    ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel> loginUserCommandHandler,
    IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel> exchangeRefreshTokenCommandHandler,
    IDeleteItemCommandHandler<LogoutUserCommand, DeleteRecordViewModel> logoutUserCommandHandler,
    ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel> resetPasswordCommandHandler,
    IUpdateItemCommandHandler<UpdatePasswordCommand, UpdatePasswordViewModel> updatePasswordCommandHandler,

    IRegisteredAppResolver registeredAppResolver
    ) : BaseController
{
  private readonly IGetItemQueryHandler<GetUserQuery, UserViewModel> _getUserQueryHandler = getUserQueryHandler;
  private readonly ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel> _registerUserCommandHandler = registerUserComandHandler;
  private readonly ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel> _loginUserCommandHandler = loginUserCommandHandler;
  private readonly IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel> _exchangeRefreshTokenCommandHandler = exchangeRefreshTokenCommandHandler;
  private readonly IDeleteItemCommandHandler<LogoutUserCommand, DeleteRecordViewModel> _logoutUserCommandHandler = logoutUserCommandHandler;
  private readonly ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel> _resetPasswordCommandHandler = resetPasswordCommandHandler;
  private readonly IUpdateItemCommandHandler<UpdatePasswordCommand, UpdatePasswordViewModel> _updatePasswordCommandHandler = updatePasswordCommandHandler;

  private readonly IRegisteredAppResolver _registeredAppResolver = registeredAppResolver;


  [Authorize(Policy = ZenConstants.ADMIN_GLOBAL_ADMIN_OR_SAME_USER_POLICY)]
  [HttpGet]
  [Route("{userId}")]
  public async Task<ActionResult<UserViewModel>> GetUser(string userId)
  {
    var vm = await _getUserQueryHandler.GetItemAsync(new GetUserQuery { UserId = userId, AppName = IsGlobalAdmin ? null : AuthorizedParty }, RequestAborted);
    return ResolveGetItemActionResult(vm, vm.User.OwnedByLoggedInUser(LoggedInUserId));
  }

  [AllowAnonymous]
  [EnableRateLimiting(RateLimitSettings.ClientIpPolicy)]
  [HttpPost]
  [Route("register")]
  public async Task<ActionResult<AuthUserViewModel>> CreateUser(RegisterUserCommand command)
  {
    command.RegisteredApp = await GetRegisteredAppAsync(_registeredAppResolver);

    var vm = await _registerUserCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);
    return ResolveCreateItemActionResult(vm, true);
  }

  [Authorize(Policy = ZenConstants.USER_SCOPE_POLICY, Roles = ZenConstants.INTERNAL_SERVICE_ROLE)]
  [HttpPost]
  [Route("register-for-service")]
  public async Task<ActionResult<AuthUserViewModel>> CreateUserForService(RegisterUserCommand command)
  {
    command.RegisteredApp = await GetRegisteredAppAsync(_registeredAppResolver);
    command.RequestSource = RequestSource.API;

    var vm = await _registerUserCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);
    return ResolveCreateItemActionResult(vm, true);
  }

  [AllowAnonymous]
  [EnableRateLimiting(RateLimitSettings.ClientIpPolicy)]
  [HttpPost]
  [Route("login")]
  public async Task<ActionResult<AuthUserViewModel>> LoginUser(LoginUserCommand command)
  {
    command.RegisteredApp = await GetRegisteredAppAsync(_registeredAppResolver);

    var vm = await _loginUserCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);

    vm.ResolveEditDeleteRights(UserScopeClaims, ScopeResource, true);
    vm.ResolveRequestStatus(RequestStatus.SUCCESSFUL, ItemStatusMessage.FETCH_ITEM_SUCCESSFUL, "Login successful");

    return StatusCode((int)HttpStatusCode.OK, vm);
  }

  [AllowAnonymous]
  [HttpPost]
  [Route("refresh-token")]
  public async Task<ActionResult<ExchangeRefreshTokenViewModel>> RefreshToken(ExchangeRefreshTokenCommand command)
  {
    var vm = await _exchangeRefreshTokenCommandHandler.UpdateItemAsync(command, LoggedInUserId, RequestAborted);

    vm.EditEnabled = true;
    vm.ResolveRequestStatus(RequestStatus.SUCCESSFUL, ItemStatusMessage.FETCH_ITEM_SUCCESSFUL, "Refresh token generated successfully");
    return StatusCode((int)HttpStatusCode.OK, vm);
  }

  [HttpPost]
  [Route("logout")]
  public async Task<ActionResult<DeleteRecordViewModel>> LogoutUser(LogoutUserCommand command)
  {
    var vm = await _logoutUserCommandHandler.DeleteItemAsync(command, LoggedInUserId, RequestAborted);

    vm.ResolveRequestStatus(RequestStatus.SUCCESSFUL, ItemStatusMessage.DELETE_ITEM_SUCCESSFUL, "Logout successful");
    return StatusCode((int)HttpStatusCode.OK, vm);
  }

  [AllowAnonymous]
  [EnableRateLimiting(RateLimitSettings.ClientIpPolicy)]
  [HttpPost]
  [Route("reset-password")]
  public async Task<ActionResult<ResetPasswordViewModel>> ResetPassword(ResetPasswordCommand command)
  {
    command.RegisteredApp = await GetRegisteredAppAsync(_registeredAppResolver);

    var vm = await _resetPasswordCommandHandler.CreateItemAsync(command, LoggedInUserId, RequestAborted);
    vm.ResolveRequestStatus(RequestStatus.SUCCESSFUL, ItemStatusMessage.FETCH_ITEM_SUCCESSFUL, "Password reset request successful");
    return StatusCode((int)HttpStatusCode.OK, vm);
  }

  [AllowAnonymous]
  [EnableRateLimiting(RateLimitSettings.ClientIpPolicy)]
  [HttpPost]
  [Route("update-password")]
  public async Task<ActionResult<UpdatePasswordViewModel>> UpdatePassword(UpdatePasswordCommand command)
  {
    var vm = await _updatePasswordCommandHandler.UpdateItemAsync(command, LoggedInUserId, RequestAborted);

    var httpStatusCode = HttpStatusCode.OK;
    if (vm.UpdatePasswordResult.PassWordUpdated)
    {
      vm.ResolveRequestStatus(RequestStatus.SUCCESSFUL, ItemStatusMessage.UPDATE_ITEM_SUCCESSFUL, "Password updated successfully");
    }
    else
    {
      vm.ResolveRequestStatus(RequestStatus.FAILED, ItemStatusMessage.FETCH_ITEM_FAILED, "Password update failed");
      httpStatusCode = HttpStatusCode.NotAcceptable;
    }
    return StatusCode((int)httpStatusCode, vm);
  }
}
