using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Presentation.Endpoints;

namespace UserIdentity.Presentation.Endpoints;

internal static class AdminApp
{
    // App admins work on their token's app, and global admins name the app in the X-App-Name header
    internal static Task<RegisteredAppEntity> GetAsync(
        ApiRequest request,
        IRegisteredAppResolver resolver,
        CancellationToken cancellationToken
    ) =>
        request.IsGlobalAdmin
            ? request.GetRegisteredAppAsync(resolver, cancellationToken)
            : resolver.GetActiveAppAsync(request.AuthorizedParty, cancellationToken);
}
