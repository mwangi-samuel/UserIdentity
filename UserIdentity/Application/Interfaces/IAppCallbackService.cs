using PolyzenKit.Domain.Entities.RegisteredApps;

using UserIdentity.Application.Core.Users.Events;

namespace UserIdentity.Application.Interfaces;

public interface IAppCallbackService
{
  Task SendCallbackRequestAsync(
    RegisteredAppEntity registeredApp,
    UserRegisteredEvent user,
    CancellationToken cancellationToken = default);
}
