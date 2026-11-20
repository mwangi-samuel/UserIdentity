using Microsoft.AspNetCore.Http.HttpResults;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Endpoints;
using UserIdentity.Application.Core.SignInSettings.Commands;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;

namespace UserIdentity.Presentation.Endpoints;

public static class SignInSettingsEndpoints
{
    public static RouteGroupBuilder MapSignInSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapApiGroup("SignInSettings").RequireAuthorization(AuthorizationPolicies.GlobalAdmin);

        group.MapGet("", GetSignInSettingsAsync).Produces<SignInSettingsViewModel>();
        group.MapPut("", UpdateSignInSettingsAsync).Produces<SignInSettingsViewModel>();

        return group;
    }

    internal static async Task<JsonHttpResult<SignInSettingsViewModel>> GetSignInSettingsAsync(
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        var registeredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);

        var vm = await handler.GetItemAsync(new GetSignInSettingsQuery(registeredApp.Id), cancellationToken);

        return ViewModelResults.GetItem(request, vm, true);
    }

    internal static async Task<JsonHttpResult<SignInSettingsViewModel>> UpdateSignInSettingsAsync(
        UpdateSignInSettingsCommand command,
        ApiRequest request,
        IRegisteredAppResolver registeredAppResolver,
        IUpdateItemCommandHandler<UpdateSignInSettingsCommand, SignInSettingsViewModel> handler,
        CancellationToken cancellationToken
    )
    {
        command.RegisteredApp = await AdminApp.GetAsync(request, registeredAppResolver, cancellationToken);

        var vm = await handler.UpdateItemAsync(command, request.LoggedInUserId, cancellationToken);

        return ViewModelResults.UpdateItem(request, vm, true);
    }
}
