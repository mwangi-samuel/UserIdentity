using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Presentation.Controllers;

namespace UserIdentity.Presentation.Controllers;

public abstract class AppAdminController(
  IRegisteredAppResolver registeredAppResolver
  ) : BaseController
{
  protected readonly IRegisteredAppResolver _registeredAppResolver = registeredAppResolver;

  // App admins work on their token's app, and global admins name the app in the X-App-Name header
  protected Task<RegisteredAppEntity> GetAdminAppAsync() =>
    IsGlobalAdmin ? GetRegisteredAppAsync(_registeredAppResolver) : _registeredAppResolver.GetActiveAppAsync(AuthorizedParty, RequestAborted);
}
