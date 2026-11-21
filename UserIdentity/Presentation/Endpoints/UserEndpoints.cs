using Microsoft.AspNetCore.Http.HttpResults;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Dtos;
using PolyzenKit.Presentation.Captcha;
using PolyzenKit.Presentation.Endpoints;
using PolyzenKit.Presentation.Settings;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Presentation.Filters;

namespace UserIdentity.Presentation.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapApiGroup("User").RequireAuthorization();

        group
            .MapGet("{userId}", GetUserAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminGlobalAdminOrSameUser)
            .Produces<UserViewModel>();
        group
            .MapGet("email/{email}", GetUserByEmailAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .Produces<UserViewModel>();
        group
            .MapPut("{userId}", UpdateUserAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminGlobalAdminOrSameUser)
            .Produces<UserViewModel>();
        group
            .MapPost("{userId}/email", ChangeEmailAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminGlobalAdminOrSameUser)
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<EmailChangeViewModel>();
        group
            .MapPost("{userId}/email/confirm", ConfirmEmailChangeAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminGlobalAdminOrSameUser)
            .Produces<UserViewModel>();
        group
            .MapPost("register", CreateUserAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .RequireCaptcha("register")
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<AuthUserViewModel>();
        group
            .MapPost("register-for-service", CreateUserForServiceAsync)
            .RequireAuthorization(AuthorizationPolicies.AppBackend)
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<UserViewModel>();
        group
            .MapPost("login", LoginUserAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<AuthUserViewModel>();
        group.MapPost("refresh-token", ExchangeRefreshTokenAsync).AllowAnonymous().Produces<ExchangeRefreshTokenViewModel>();
        group
            .MapPost("confirm", ConfirmAccountAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .Produces<AuthUserViewModel>();
        group
            .MapPost("resend-code", ResendCodeAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .RequireCaptcha("resend-code")
            .Produces<ResendCodeViewModel>();
        group.MapPost("logout", LogoutUserAsync).Produces<DeleteRecordViewModel>();
        group
            .MapPost("reset-password", ResetPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .RequireCaptcha("reset-password")
            .Produces<ResetPasswordViewModel>();
        group
            .MapPost("update-password", UpdatePasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<AuthUserViewModel>();
        group.MapGet("two-factor", GetTwoFactorStatusAsync).Produces<TwoFactorStatusViewModel>();
        group
            .MapPost("two-factor/verify", VerifySecondFactorAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .Produces<AuthUserViewModel>();
        group
            .MapPost("two-factor/setup", StartTwoFactorSetupAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .Produces<TwoFactorSetupViewModel>();
        group
            .MapPost("two-factor/setup/confirm", ConfirmTwoFactorSetupAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .Produces<AuthUserViewModel>();
        group.MapPost("two-factor/change", ChangeTwoFactorAsync).AddEndpointFilter<PasswordHashingFilter>().Produces<AuthUserViewModel>();
        group
            .MapPost("two-factor/recovery-codes", ReplaceRecoveryCodesAsync)
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<RecoveryCodesViewModel>();
        group
            .MapPost("two-factor/disable", DisableTwoFactorAsync)
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<TwoFactorStatusViewModel>();
        group
            .MapPost("two-factor/reset", ResetUserTwoFactorAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .Produces<UserViewModel>();

        return group;
    }

    internal static async Task<JsonHttpResult<UserViewModel>> GetUserAsync(
        string userId,
        ApiRequest request,
        IGetItemQueryHandler<GetUserQuery, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.GetItemAsync(new GetUserQuery { UserId = userId, AppName = AdminApp.GetName(request) }, cancellationToken);

        return ViewModelResults.GetItem(request, vm, vm.User.OwnedByLoggedInUser(request.LoggedInUserId));
    }

    internal static async Task<JsonHttpResult<UserViewModel>> GetUserByEmailAsync(
        string email,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        IGetItemQueryHandler<GetUserByEmailQuery, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.GetItemAsync(
            new GetUserByEmailQuery
            {
                Email = email,
                RegisteredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken),
                Changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin),
            },
            cancellationToken
        );

        return ViewModelResults.GetItem(request, vm, vm.User.OwnedByLoggedInUser(request.LoggedInUserId));
    }

    internal static async Task<JsonHttpResult<UserViewModel>> UpdateUserAsync(
        string userId,
        UpdateUserCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        IUpdateItemCommandHandler<UpdateUserCommand, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = userId;
        command.RegisteredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);
        command.Changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.UpdateItem(request, vm, vm.User.OwnedByLoggedInUser(request.LoggedInUserId));
    }

    internal static async Task<JsonHttpResult<EmailChangeViewModel>> ChangeEmailAsync(
        string userId,
        ChangeEmailCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<ChangeEmailCommand, EmailChangeViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = userId;
        command.RegisteredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);
        command.Changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.UpdateItem(request, vm, vm.User.OwnedByLoggedInUser(request.LoggedInUserId));
    }

    internal static async Task<JsonHttpResult<UserViewModel>> ConfirmEmailChangeAsync(
        string userId,
        ConfirmEmailChangeCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<ConfirmEmailChangeCommand, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = userId;
        command.RegisteredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);
        command.Changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.UpdateItem(request, vm, vm.User.OwnedByLoggedInUser(request.LoggedInUserId));
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> CreateUserAsync(
        RegisterUserCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.CreateItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<UserViewModel>> CreateUserForServiceAsync(
        RegisterServiceUserCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<RegisterServiceUserCommand, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await registeredAppResolver.GetActiveAppAsync(request.AuthorizedParty, cancellationToken);

        var vm = await handler.CreateItemAsync(command, KnownUsers.SystemId, cancellationToken);

        return ViewModelResults.CreateItem(request, vm);
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> LoginUserAsync(
        LoginUserCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true, "Login successful");
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> ConfirmAccountAsync(
        ConfirmAccountCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<ConfirmAccountCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true, "Account confirmed");
    }

    internal static async Task<JsonHttpResult<ResendCodeViewModel>> ResendCodeAsync(
        ResendCodeCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<ResendCodeCommand, ResendCodeViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.Status(
            vm,
            StatusCodes.Status200OK,
            RequestStatus.Successful,
            ItemStatusMessage.CreateItemSuccessful,
            "Code resend requested"
        );
    }

    internal static async Task<JsonHttpResult<ExchangeRefreshTokenViewModel>> ExchangeRefreshTokenAsync(
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

    internal static async Task<JsonHttpResult<DeleteRecordViewModel>> LogoutUserAsync(
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

    internal static async Task<JsonHttpResult<ResetPasswordViewModel>> ResetPasswordAsync(
        ResetPasswordCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.Status(
            vm,
            StatusCodes.Status200OK,
            RequestStatus.Successful,
            ItemStatusMessage.FetchItemSuccessful,
            "Password reset request successful"
        );
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> UpdatePasswordAsync(
        UpdatePasswordCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        IUpdateItemCommandHandler<UpdatePasswordCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true, "Password updated successfully");
    }

    internal static async Task<JsonHttpResult<TwoFactorStatusViewModel>> GetTwoFactorStatusAsync(
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var registeredApp = await registeredAppResolver.GetActiveAppAsync(request.AuthorizedParty, cancellationToken);

        var vm = await handler.GetItemAsync(new GetTwoFactorStatusQuery(request.LoggedInUserId, registeredApp), cancellationToken);

        return ViewModelResults.GetItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> VerifySecondFactorAsync(
        VerifySecondFactorCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<VerifySecondFactorCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true, "Second step passed");
    }

    internal static async Task<JsonHttpResult<TwoFactorSetupViewModel>> StartTwoFactorSetupAsync(
        StartTwoFactorSetupCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<StartTwoFactorSetupCommand, TwoFactorSetupViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.Status(vm, StatusCodes.Status200OK, RequestStatus.Successful, ItemStatusMessage.CreateItemSuccessful);
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> ConfirmTwoFactorSetupAsync(
        ConfirmTwoFactorSetupCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<ConfirmTwoFactorSetupCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true, "Two-step sign-in set up");
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> ChangeTwoFactorAsync(
        ChangeTwoFactorCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<ChangeTwoFactorCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = request.LoggedInUserId;
        command.RegisteredApp = await registeredAppResolver.GetActiveAppAsync(request.AuthorizedParty, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<RecoveryCodesViewModel>> ReplaceRecoveryCodesAsync(
        ReplaceRecoveryCodesCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<ReplaceRecoveryCodesCommand, RecoveryCodesViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = request.LoggedInUserId;
        command.RegisteredApp = await registeredAppResolver.GetActiveAppAsync(request.AuthorizedParty, cancellationToken);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.Status(vm, StatusCodes.Status200OK, RequestStatus.Successful, ItemStatusMessage.CreateItemSuccessful);
    }

    internal static async Task<JsonHttpResult<TwoFactorStatusViewModel>> DisableTwoFactorAsync(
        DisableTwoFactorCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IRegisteredAppResolver registeredAppResolver,
        IUpdateItemCommandHandler<DisableTwoFactorCommand, TwoFactorStatusViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = request.LoggedInUserId;
        command.RegisteredApp = await registeredAppResolver.GetActiveAppAsync(request.AuthorizedParty, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.UpdateItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<UserViewModel>> ResetUserTwoFactorAsync(
        ResetUserTwoFactorCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        IUpdateItemCommandHandler<ResetUserTwoFactorCommand, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);
        command.Changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin);

        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.UpdateItem(request, vm);
    }
}
