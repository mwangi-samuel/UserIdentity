using PolyzenKit.Application.Core.RegisteredApps.Shared;
using UserIdentity.Application.Core.Users.Events;

namespace UserIdentity.Application.Interfaces;

public interface IAppCallbackService
{
    Task SendCallbackRequestAsync(
        ActiveRegisteredApp registeredApp,
        UserRegisteredEvent user,
        CancellationToken cancellationToken = default
    );
}
