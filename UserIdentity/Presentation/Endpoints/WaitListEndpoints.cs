using Microsoft.AspNetCore.Http.HttpResults;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Endpoints;
using PolyzenKit.Presentation.Settings;
using UserIdentity.Application.Core.WaitLists.Commands;
using UserIdentity.Application.Core.WaitLists.Queries;
using UserIdentity.Application.Core.WaitLists.ViewModels;

namespace UserIdentity.Presentation.Endpoints;

public static class WaitListEndpoints
{
    public static RouteGroupBuilder MapWaitListEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapApiGroup("WaitList");

        group
            .MapGet("{waitListId}", GetWaitListAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .Produces<WaitListViewModel>();
        group
            .MapGet("email/{email}", GetWaitListByEmailAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOrGlobalAdmin)
            .Produces<WaitListViewModel>();
        group
            .MapPost("", CreateWaitListAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitSettings.ClientIpPolicy)
            .Produces<WaitListViewModel>();

        return group;
    }

    internal static async Task<JsonHttpResult<WaitListViewModel>> GetWaitListAsync(
        long waitListId,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var registeredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);

        var vm = await handler.GetItemAsync(new GetWaitListQuery { WaitListId = waitListId, AppId = registeredApp.Id }, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<WaitListViewModel>> GetWaitListByEmailAsync(
        string email,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var registeredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);

        var vm = await handler.GetItemAsync(new GetWaitListQuery { UserEmail = email, AppId = registeredApp.Id }, cancellationToken);

        return ViewModelResults.GetItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<WaitListViewModel>> CreateWaitListAsync(
        CreateWaitListCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var registeredApp = await request.GetRegisteredAppAsync(registeredAppResolver, cancellationToken);

        command.AppId = registeredApp.Id;

        var vm = await handler.CreateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.CreateItem(request, vm, true);
    }
}
