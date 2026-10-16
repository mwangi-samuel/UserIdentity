using UserIdentity.Application.Core.Users.Events;

namespace UserIdentity.Application.Interfaces;

public interface IAppCallbackService
{
  Task SendCallbackRequestAsync(UserUpdateEvent userUpdateEvent, CancellationToken cancellationToken = default);
}
