using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;

using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Interfaces;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Events;

public class UserRegisteredEventHandlerTests
{
  private readonly IRegisteredAppResolver _registeredAppResolver = A.Fake<IRegisteredAppResolver>();
  private readonly IAppCallbackService _appCallbackService = A.Fake<IAppCallbackService>();

  private static readonly UserRegisteredEvent _event = new() { AppName = "TestApp", UserId = "user-id" };

  [Fact]
  public async Task HandleEventAsync_For_An_App_With_A_Callback_Url_Sends_The_Callback()
  {
    // Arrange
    var app = new RegisteredAppEntity { AppName = "TestApp", CallbackUrl = "https://callback.test" };
    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync("TestApp", A<CancellationToken>._)).Returns(app);

    // Act
    await CreateHandler().HandleEventAsync(_event, TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => _appCallbackService.SendCallbackRequestAsync(app, _event, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task HandleEventAsync_For_An_App_That_No_Longer_Has_A_Callback_Url_Sends_Nothing()
  {
    // Arrange
    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync("TestApp", A<CancellationToken>._)).Returns(new RegisteredAppEntity { AppName = "TestApp" });

    // Act
    await CreateHandler().HandleEventAsync(_event, TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => _appCallbackService.SendCallbackRequestAsync(A<RegisteredAppEntity>._, A<UserRegisteredEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task HandleEventAsync_For_A_Missing_App_Lets_NoRecordException_Through()
  {
    // Arrange
    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync("TestApp", A<CancellationToken>._)).ThrowsAsync(NoRecordException.ForIdentifier("TestApp"));

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => CreateHandler().HandleEventAsync(_event, TestContext.Current.CancellationToken));
  }

  private UserRegisteredEventHandler CreateHandler() => new(_registeredAppResolver, _appCallbackService);
}
