using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;

using UserIdentity.Application.Interfaces;

namespace UserIdentity.Application.Core.Users.Events;

public record UserRegisteredEvent : IBaseEvent
{
  public required string AppName { get; init; }

  public required string UserId { get; init; }

  public string? FirstName { get; init; }

  public string? LastName { get; init; }

  public string? UserName { get; init; }

  public string? PhoneNumber { get; init; }

  public string? UserEmail { get; init; }
}

public class UserRegisteredEventHandler(
  IRegisteredAppResolver registeredAppResolver,
  IAppCallbackService appCallbackService
  ) : IEventHandler<UserRegisteredEvent>
{
  private readonly IRegisteredAppResolver _registeredAppResolver = registeredAppResolver;
  private readonly IAppCallbackService _appCallbackService = appCallbackService;

  public async Task HandleEventAsync(UserRegisteredEvent eventItem, CancellationToken cancellationToken = default)
  {
    var registeredApp = await _registeredAppResolver.GetActiveAppAsync(eventItem.AppName, cancellationToken);

    if (registeredApp.CallbackUrl == null)
      return;

    await _appCallbackService.SendCallbackRequestAsync(registeredApp, eventItem, cancellationToken);
  }
}
