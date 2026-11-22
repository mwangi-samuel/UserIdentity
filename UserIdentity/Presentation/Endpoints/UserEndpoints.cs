using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Queries;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Paging;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Dtos;
using PolyzenKit.Presentation.Captcha;
using PolyzenKit.Presentation.Endpoints;
using PolyzenKit.Presentation.Settings;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Queries;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.Roles;
using UserIdentity.Domain.Users;
using UserIdentity.Presentation.Filters;

namespace UserIdentity.Presentation.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapApiGroup("User").RequireAuthorization();

        group.MapGet("", GetUsersAsync).RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin).Produces<UsersViewModel>();
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
            .MapDelete("{userId}", DeleteUserAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminGlobalAdminOrSameUser)
            .AddEndpointFilter<PasswordHashingFilter>()
            .Produces<DeleteRecordViewModel>();
        group
            .MapPost("step-up/code", SendStepUpCodeAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .Produces<StepUpCodeViewModel>();
        group
            .MapPost("step-up", CreateStepUpAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .Produces<StepUpViewModel>();
        group
            .MapGet("{userId}/sessions", GetSessionsAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminGlobalAdminOrSameUser)
            .Produces<SessionsViewModel>();
        group
            .MapDelete("{userId}/sessions/{sessionId:guid}", EndSessionAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminGlobalAdminOrSameUser)
            .Produces<DeleteRecordViewModel>();
        group
            .MapDelete("{userId}/sessions", EndSessionsAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminGlobalAdminOrSameUser)
            .Produces<DeleteRecordViewModel>();
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

    internal static async Task<JsonHttpResult<UsersViewModel>> GetUsersAsync(
        ApiRequest request,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IGetItemsQueryHandler<GetUsersQuery, UsersViewModel> handler,
        CancellationToken cancellationToken,
        [FromQuery, Range(1, PagingLimits.MaxPage)] int page = 1,
        [FromQuery, Range(1, PagingLimits.MaxPageSize)] int pageSize = PagingLimits.DefaultPageSize,
        [FromQuery, MaxLength(UserLimits.SearchMaxLength)] string? search = null,
        [FromQuery, MaxLength(RoleLimits.NameMaxLength)] string? role = null,
        [FromQuery] bool? emailConfirmed = null
    )
    {
        var query = new GetUsersQuery
        {
            RegisteredApp = await AdminApp.GetAsync(request, getRegisteredAppHandler, cancellationToken),
            Changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin),
            Search = search,
            Role = role,
            EmailConfirmed = emailConfirmed,
            Page = page,
            PageSize = pageSize,
        };

        var vm = await handler.GetItemsAsync(query, cancellationToken);

        return ViewModelResults.GetItems(request, vm);
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
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IGetItemQueryHandler<GetUserByEmailQuery, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.GetItemAsync(
            new GetUserByEmailQuery
            {
                Email = email,
                RegisteredApp = await AdminApp.GetAsync(request, getRegisteredAppHandler, cancellationToken),
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
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IUpdateItemCommandHandler<UpdateUserCommand, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = userId;
        command.RegisteredApp = await AdminApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);
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
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<ChangeEmailCommand, EmailChangeViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = userId;
        command.RegisteredApp = await AdminApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);
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
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<ConfirmEmailChangeCommand, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = userId;
        command.RegisteredApp = await AdminApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);
        command.Changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.UpdateItem(request, vm, vm.User.OwnedByLoggedInUser(request.LoggedInUserId));
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> CreateUserAsync(
        RegisterUserCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(getRegisteredAppHandler, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.CreateItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<UserViewModel>> CreateUserForServiceAsync(
        RegisterServiceUserCommand command,
        ApiRequest request,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<RegisterServiceUserCommand, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await TokenApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);

        var vm = await handler.CreateItemAsync(command, KnownUsers.SystemId, cancellationToken);

        return ViewModelResults.CreateItem(request, vm);
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> LoginUserAsync(
        LoginUserCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(getRegisteredAppHandler, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true, "Login successful");
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> ConfirmAccountAsync(
        ConfirmAccountCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<ConfirmAccountCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(getRegisteredAppHandler, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true, "Account confirmed");
    }

    internal static async Task<JsonHttpResult<ResendCodeViewModel>> ResendCodeAsync(
        ResendCodeCommand command,
        ApiRequest request,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<ResendCodeCommand, ResendCodeViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(getRegisteredAppHandler, cancellationToken);

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
        HttpContext httpContext,
        IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RequestContext = RequestContextReader.Read(httpContext);

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
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(getRegisteredAppHandler, cancellationToken);
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
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IUpdateItemCommandHandler<UpdatePasswordCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(getRegisteredAppHandler, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true, "Password updated successfully");
    }

    internal static async Task<JsonHttpResult<TwoFactorStatusViewModel>> GetTwoFactorStatusAsync(
        ApiRequest request,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var registeredApp = await TokenApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);

        var vm = await handler.GetItemAsync(new GetTwoFactorStatusQuery(request.LoggedInUserId, registeredApp), cancellationToken);

        return ViewModelResults.GetItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> VerifySecondFactorAsync(
        VerifySecondFactorCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<VerifySecondFactorCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(getRegisteredAppHandler, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true, "Second step passed");
    }

    internal static async Task<JsonHttpResult<TwoFactorSetupViewModel>> StartTwoFactorSetupAsync(
        StartTwoFactorSetupCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<StartTwoFactorSetupCommand, TwoFactorSetupViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(getRegisteredAppHandler, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.Status(vm, StatusCodes.Status200OK, RequestStatus.Successful, ItemStatusMessage.CreateItemSuccessful);
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> ConfirmTwoFactorSetupAsync(
        ConfirmTwoFactorSetupCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<ConfirmTwoFactorSetupCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await request.GetRegisteredAppAsync(getRegisteredAppHandler, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true, "Two-step sign-in set up");
    }

    internal static async Task<JsonHttpResult<AuthUserViewModel>> ChangeTwoFactorAsync(
        ChangeTwoFactorCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<ChangeTwoFactorCommand, AuthUserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = request.LoggedInUserId;
        command.RegisteredApp = await TokenApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<RecoveryCodesViewModel>> ReplaceRecoveryCodesAsync(
        ReplaceRecoveryCodesCommand command,
        ApiRequest request,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<ReplaceRecoveryCodesCommand, RecoveryCodesViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = request.LoggedInUserId;
        command.RegisteredApp = await TokenApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.Status(vm, StatusCodes.Status200OK, RequestStatus.Successful, ItemStatusMessage.CreateItemSuccessful);
    }

    internal static async Task<JsonHttpResult<TwoFactorStatusViewModel>> DisableTwoFactorAsync(
        DisableTwoFactorCommand command,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IUpdateItemCommandHandler<DisableTwoFactorCommand, TwoFactorStatusViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = request.LoggedInUserId;
        command.RegisteredApp = await TokenApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);
        command.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.UpdateItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<UserViewModel>> ResetUserTwoFactorAsync(
        ResetUserTwoFactorCommand command,
        ApiRequest request,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IUpdateItemCommandHandler<ResetUserTwoFactorCommand, UserViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await AdminApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);
        command.Changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin);

        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.UpdateItem(request, vm);
    }

    internal static async Task<JsonHttpResult<DeleteRecordViewModel>> DeleteUserAsync(
        string userId,
        [FromBody] DeleteUserCommand? command,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IDeleteItemCommandHandler<DeleteUserCommand, DeleteRecordViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var deleteCommand = command ?? new DeleteUserCommand();

        deleteCommand.UserId = userId;
        deleteCommand.RegisteredApp = await AdminApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);
        deleteCommand.Changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin);
        deleteCommand.ChangerAppId = (await TokenApp.GetAsync(request, getRegisteredAppHandler, cancellationToken)).Id;
        deleteCommand.RequestContext = RequestContextReader.Read(httpContext);

        var vm = await handler.DeleteItemAsync(deleteCommand, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.DeleteItem(vm);
    }

    internal static async Task<JsonHttpResult<StepUpCodeViewModel>> SendStepUpCodeAsync(
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<SendStepUpCodeCommand, StepUpCodeViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var registeredApp = await TokenApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);

        var vm = await handler.CreateItemAsync(
            new SendStepUpCodeCommand(request.LoggedInUserId, registeredApp, RequestContextReader.Read(httpContext)),
            request.LoggedInUserId,
            cancellationToken
        );

        return ViewModelResults.Status(vm, StatusCodes.Status200OK, RequestStatus.Successful, ItemStatusMessage.CreateItemSuccessful);
    }

    internal static async Task<JsonHttpResult<StepUpViewModel>> CreateStepUpAsync(
        CreateStepUpCommand command,
        ApiRequest request,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        ICreateItemCommandHandler<CreateStepUpCommand, StepUpViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.UserId = request.LoggedInUserId;
        command.RegisteredApp = await TokenApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.Status(vm, StatusCodes.Status200OK, RequestStatus.Successful, ItemStatusMessage.CreateItemSuccessful);
    }

    internal static async Task<JsonHttpResult<SessionsViewModel>> GetSessionsAsync(
        string userId,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IGetItemsQueryHandler<GetSessionsQuery, SessionsViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.GetItemsAsync(
            new GetSessionsQuery
            {
                UserId = userId,
                RegisteredApp = await AdminApp.GetAsync(request, getRegisteredAppHandler, cancellationToken),
                Changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin),
                CurrentSessionId = CurrentSessionId(httpContext),
            },
            cancellationToken
        );

        return ViewModelResults.GetItems(request, vm, userId == request.LoggedInUserId);
    }

    internal static async Task<JsonHttpResult<DeleteRecordViewModel>> EndSessionAsync(
        string userId,
        Guid sessionId,
        ApiRequest request,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IDeleteItemCommandHandler<EndSessionCommand, DeleteRecordViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var registeredApp = await AdminApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);
        var changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin);

        var vm = await handler.DeleteItemAsync(
            new EndSessionCommand(userId, sessionId, registeredApp, changer),
            request.LoggedInUserId,
            cancellationToken
        );

        return ViewModelResults.DeleteItem(vm);
    }

    internal static async Task<JsonHttpResult<DeleteRecordViewModel>> EndSessionsAsync(
        string userId,
        ApiRequest request,
        HttpContext httpContext,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        IDeleteItemCommandHandler<EndSessionsCommand, DeleteRecordViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var registeredApp = await AdminApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);
        var changer = new UserChanger(request.LoggedInUserId, request.IsGlobalAdmin);

        var vm = await handler.DeleteItemAsync(
            new EndSessionsCommand(userId, registeredApp, changer, CurrentSessionId(httpContext)),
            request.LoggedInUserId,
            cancellationToken
        );

        return ViewModelResults.DeleteItem(vm);
    }

    private static Guid? CurrentSessionId(HttpContext httpContext) =>
        Guid.TryParse(httpContext.User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value, out var sessionId) ? sessionId : null;
}
