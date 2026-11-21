using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Queries;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Presentation.Endpoints;

namespace UserIdentity.Presentation.Endpoints;

internal static class TokenApp
{
    internal static async Task<RegisteredAppDto> GetAsync(
        ApiRequest request,
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> getRegisteredAppHandler,
        CancellationToken cancellationToken
    ) =>
        (
            await getRegisteredAppHandler.GetItemAsync(new GetRegisteredAppQuery { AppName = request.AuthorizedParty }, cancellationToken)
        ).RegisteredApp;
}
