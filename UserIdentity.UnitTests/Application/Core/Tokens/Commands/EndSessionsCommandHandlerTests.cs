using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class EndSessionsCommandHandlerTests
{
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new() { Id = "user-1" };

    public EndSessionsCommandHandlerTests()
    {
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:user"]);
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(true);
    }

    [Fact]
    public async Task Ending_The_Sessions_Ends_The_Users_Other_Sessions_In_The_App_And_Keeps_The_Callers()
    {
        // Arrange
        var currentSessionId = Guid.CreateVersion7();

        // Act
        await GetHandler()
            .DeleteItemAsync(
                new EndSessionsCommand(_user.Id, _registeredApp, new UserChanger(_user.Id, false), currentSessionId),
                _user.Id,
                TestContext.Current.CancellationToken
            );

        // Assert
        A.CallTo(() =>
                _refreshTokenRepository.DeleteEntityItemsAsync(_user.Id, _registeredApp.Id, null, currentSessionId, A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Ending_The_Sessions_Of_An_Admin_Account_As_An_App_Admin_Is_Not_Found_And_Ends_Nothing()
    {
        // Arrange
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:admin"]);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler()
                .DeleteItemAsync(
                    new EndSessionsCommand(_user.Id, _registeredApp, new UserChanger("admin-id", false), Guid.CreateVersion7()),
                    "admin-id",
                    TestContext.Current.CancellationToken
                )
        );

        A.CallTo(() =>
                _refreshTokenRepository.DeleteEntityItemsAsync(A<string>._, A<int?>._, A<Guid?>._, A<Guid?>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }

    private EndSessionsCommandHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _userManager,
            _userRegisteredAppRepository,
            _refreshTokenRepository
        );
}
