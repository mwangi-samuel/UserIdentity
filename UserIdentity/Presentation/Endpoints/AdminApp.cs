using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Queries;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Presentation.Endpoints;

namespace UserIdentity.Presentation.Endpoints;

internal static class AdminApp
{
    // App admins work on their token's app, and global admins name the app in the X-App-Name header
    internal static Task<RegisteredAppDto> GetAsync(
        ApiRequest request,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        CancellationToken cancellationToken
    ) =>
        request.IsGlobalAdmin
            ? request.GetRegisteredAppAsync(getRegisteredAppHandler, cancellationToken)
            : TokenApp.GetAsync(request, getRegisteredAppHandler, cancellationToken);

    internal static string? GetName(ApiRequest request) => request.IsGlobalAdmin ? null : request.AuthorizedParty;
}
