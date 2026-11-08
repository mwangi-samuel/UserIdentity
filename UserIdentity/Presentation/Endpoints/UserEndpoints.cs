using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Dtos;
using PolyzenKit.Presentation.Endpoints;
using PolyzenKit.Presentation.Settings;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;

namespace UserIdentity.Presentation.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapApiGroup("User").RequireAuthorization();

        group.MapGet("{userId}", GetUser).RequireAuthorization(AuthorizationPolicies.AdminGlobalAdminOrSameUser).Produces<UserViewModel>();
        group
            .MapPost("register", CreateUser)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<AuthUserViewModel>();
        group
            .MapPost("register-for-service", CreateUserForService)
            .RequireAuthorization(new AuthorizeAttribute { Policy = AuthorizationPolicies.UserScope, Roles = KnownRoles.InternalService })
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<AuthUserViewModel>();
        group
            .MapPost("login", LoginUser)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<AuthUserViewModel>();
        group.MapPost("refresh-token", RefreshToken).AllowAnonymous().Produces<ExchangeRefreshTokenViewModel>();
        group.MapPost("login-ticket", ExchangeLoginTicket).AllowAnonymous().Produces<AuthUserViewModel>();
        group.MapPost("logout", LogoutUser).Produces<DeleteRecordViewModel>();
        group
            .MapPost("reset-password", ResetPassword)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .Produces<ResetPasswordViewModel>();
        group
            .MapPost("update-password", UpdatePassword)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<UpdatePasswordViewModel>();

        return group;
    }

    internal static async Task<JsonHttpResult<UserViewModel>> GetUser(
        string userId,
        ApiRequest request,
        IGetItemQueryHandler<GetUserQuery, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.GetItemAsync(
            new GetUserQuery { UserId = userId, AppName = request.IsGlobalAdmin ? null : request.AuthorizedParty },
            cancellationToken
        );

        return ViewModelResults.GetItem(request, vm, vm.User.OwnedByLoggedInUser(request.LoggedInUserId));
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> CreateUser(
        RegisterUserCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.CreateItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> CreateUserForService(
        RegisterUserCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);
        command.RequestSource = RequestSource.Api;

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.CreateItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> LoginUser(
        LoginUserCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        vm.ResolveEditDeleteRights(request.UserScopeClaims, request.ScopeResource, true);

        return ViewModelResults.Status(
            vm,
            StatusCodes.Status200OK,
            RequestStatus.Successful,
            ItemStatusMessage.FetchItemSuccessful,
            "Login successful"
        );
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> ExchangeLoginTicket(
        ExchangeLoginTicketCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<ExchangeLoginTicketCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        vm.ResolveEditDeleteRights(request.UserScopeClaims, request.ScopeResource, true);

        return ViewModelResults.Status(
            vm,
            StatusCodes.Status200OK,
            RequestStatus.Successful,
            ItemStatusMessage.FetchItemSuccessful,
            "Login successful"
        );
    }

    internal static async Task<JsonHttpResult<ExchangeRefreshTokenViewModel>> RefreshToken(
        ExchangeRefreshTokenCommand command,
        ApiRequest request,
        IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        vm.EditEnabled = true;

        return ViewModelResults.Status(
            vm,
            StatusCodes.Status200OK,
            RequestStatus.Successful,
            ItemStatusMessage.FetchItemSuccessful,
            "Refresh token generated successfully"
        );
    }

    internal static async Task<JsonHttpResult<DeleteRecordViewModel>> LogoutUser(
        LogoutUserCommand command,
        ApiRequest request,
        IDeleteItemCommandHandler<LogoutUserCommand, DeleteRecordViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.DeleteItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.Status(
            vm,
            StatusCodes.Status200OK,
            RequestStatus.Successful,
            ItemStatusMessage.DeleteItemSuccessful,
            "Logout successful"
        );
    }

    internal static async Task<JsonHttpResult<ResetPasswordViewModel>> ResetPassword(
        ResetPasswordCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.Status(
            vm,
            StatusCodes.Status200OK,
            RequestStatus.Successful,
            ItemStatusMessage.FetchItemSuccessful,
            "Password reset request successful"
        );
    }

    internal static async Task<JsonHttpResult<UpdatePasswordViewModel>> UpdatePassword(
        UpdatePasswordCommand command,
        ApiRequest request,
        IUpdateItemCommandHandler<UpdatePasswordCommand, UpdatePasswordViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return vm.UpdatePasswordResult.PassWordUpdated
            ? ViewModelResults.Status(
                vm,
                StatusCodes.Status200OK,
                RequestStatus.Successful,
                ItemStatusMessage.UpdateItemSuccessful,
                "Password updated successfully"
            )
            : ViewModelResults.Status(
                vm,
                StatusCodes.Status406NotAcceptable,
                RequestStatus.Failed,
                ItemStatusMessage.FetchItemFailed,
                "Password update failed"
            );
    }
}
