using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Endpoints;
using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;

namespace UserIdentity.Presentation.Endpoints;

public static class RoleEndpoints
{
    public static RouteGroupBuilder MapRoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapApiGroup("Role").RequireAuthorization();

        group.MapGet("", GetRolesAsync).RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin).Produces<RolesViewModel>();
        group.MapGet("{roleId}", GetRoleAsync).RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin).Produces<RoleViewModel>();
        group.MapPost("", CreateRoleAsync).RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin).Produces<RoleViewModel>();
        group.MapPut("{roleId}", UpdateRoleAsync).RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin).Produces<RoleViewModel>();
        group
            .MapDelete("{roleId}", DeleteRoleAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .Produces<DeleteRecordViewModel>();
        group
            .MapGet("user/{userId}", GetUserRolesAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminGlobalAdminOrSameUser)
            .Produces<UserRolesViewModel>();
        group
            .MapPost("user", CreateUserRoleAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .Produces<UserRolesViewModel>();
        group
            .MapPost("claim", CreateRoleClaimAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .Produces<RoleClaimViewModel>();
        group
            .MapGet("claim/{roleId}", GetRoleClaimsAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .Produces<RoleClaimsViewModel>();
        group
            .MapDelete("claim", DeleteRoleClaimAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .Produces<DeleteRecordViewModel>();

        return group;
    }

    internal static async Task<JsonHttpResult<RolesViewModel>> GetRolesAsync(
        ApiRequest request,
        IGetItemsQueryHandler<GetRolesQuery, RolesViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.GetItemsAsync(new GetRolesQuery { AppName = AdminApp.GetName(request) }, cancellationToken);

        return ViewModelResults.GetItems(request, vm);
    }

    internal static async Task<JsonHttpResult<RoleViewModel>> GetRoleAsync(
        string roleId,
        ApiRequest request,
        IGetItemQueryHandler<GetRoleQuery, RoleViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.GetItemAsync(new GetRoleQuery { RoleId = roleId, AppName = AdminApp.GetName(request) }, cancellationToken);

        return ViewModelResults.GetItem(request, vm);
    }

    internal static async Task<JsonHttpResult<RoleViewModel>> CreateRoleAsync(
        CreateRoleCommand command,
        ApiRequest request,
        ICreateItemCommandHandler<CreateRoleCommand, RoleViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.AppName = AdminApp.GetName(request);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.CreateItem(request, vm);
    }

    internal static async Task<JsonHttpResult<RoleViewModel>> UpdateRoleAsync(
        string roleId,
        UpdateRoleCommand command,
        ApiRequest request,
        IUpdateItemCommandHandler<UpdateRoleCommand, RoleViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RoleId = roleId;
        command.AppName = AdminApp.GetName(request);

        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.UpdateItem(request, vm);
    }

    internal static async Task<JsonHttpResult<DeleteRecordViewModel>> DeleteRoleAsync(
        string roleId,
        ApiRequest request,
        IDeleteItemCommandHandler<DeleteRoleCommand, DeleteRecordViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.DeleteItemAsync(
            new DeleteRoleCommand { RoleId = roleId, AppName = AdminApp.GetName(request) },
            request.LoggedInUserId,
            cancellationToken
        );

        return ViewModelResults.DeleteItem(vm);
    }

    internal static async Task<JsonHttpResult<UserRolesViewModel>> GetUserRolesAsync(
        string userId,
        ApiRequest request,
        IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.GetItemsAsync(
            new GetUserRolesQuery { UserId = userId, AppName = AdminApp.GetName(request) },
            cancellationToken
        );

        return ViewModelResults.GetItems(request, vm);
    }

    internal static async Task<JsonHttpResult<UserRolesViewModel>> CreateUserRoleAsync(
        CreateUserRoleCommand command,
        ApiRequest request,
        ICreateItemCommandHandler<CreateUserRoleCommand, UserRolesViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.AppName = AdminApp.GetName(request);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.CreateItems(request, vm, request.IsGlobalAdmin);
    }

    internal static async Task<JsonHttpResult<RoleClaimViewModel>> CreateRoleClaimAsync(
        CreateRoleClaimCommand command,
        ApiRequest request,
        ICreateItemCommandHandler<CreateRoleClaimCommand, RoleClaimViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.AppName = AdminApp.GetName(request);

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.CreateItem(request, vm);
    }

    internal static async Task<JsonHttpResult<RoleClaimsViewModel>> GetRoleClaimsAsync(
        string roleId,
        ApiRequest request,
        IGetItemsQueryHandler<GetRoleClaimsQuery, RoleClaimsViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var vm = await handler.GetItemsAsync(
            new GetRoleClaimsQuery { RoleId = roleId, AppName = AdminApp.GetName(request) },
            cancellationToken
        );

        return ViewModelResults.GetItems(request, vm);
    }

    internal static async Task<JsonHttpResult<DeleteRecordViewModel>> DeleteRoleClaimAsync(
        [FromBody] DeleteRoleClaimCommand command,
        ApiRequest request,
        IDeleteItemCommandHandler<DeleteRoleClaimCommand, DeleteRecordViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.AppName = AdminApp.GetName(request);

        var vm = await handler.DeleteItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.DeleteItem(vm);
    }
}
