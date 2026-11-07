using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Presentation.Endpoints;

namespace UserIdentity.Presentation.Endpoints;

internal static class AdminApp
{
    // App admins work on their token's app, and global admins name the app in the X-App-Name header
    internal static Task<ActiveRegisteredApp> GetAsync(
        ApiRequest request,
        IRegisteredAppResolver resolver,
        CancellationToken cancellationToken
    ) =>
        request.IsGlobalAdmin
            ? request.GetRegisteredAppAsync(resolver, cancellationToken)
            : resolver.GetActiveAppAsync(request.AuthorizedParty, cancellationToken);
}
