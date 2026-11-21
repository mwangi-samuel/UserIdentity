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

public class EndSessionCommandHandlerTests
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
    private readonly Guid _sessionId = Guid.CreateVersion7();

    public EndSessionCommandHandlerTests()
    {
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:user"]);
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(true);
    }

    [Fact]
    public async Task Ending_A_Session_Deletes_Only_That_Session_Of_The_User_In_The_App()
    {
        // Arrange
        A.CallTo(() =>
                _refreshTokenRepository.DeleteEntityItemsAsync(_user.Id, _registeredApp.Id, _sessionId, null, A<CancellationToken>._)
            )
            .Returns(1);

        // Act
        await GetHandler().DeleteItemAsync(GetCommand(), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _refreshTokenRepository.DeleteEntityItemsAsync(_user.Id, _registeredApp.Id, _sessionId, null, A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Ending_A_Session_That_Isnt_There_Is_Not_Found()
    {
        // Arrange
        A.CallTo(() =>
                _refreshTokenRepository.DeleteEntityItemsAsync(A<string>._, A<int?>._, A<Guid?>._, A<Guid?>._, A<CancellationToken>._)
            )
            .Returns(0);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler().DeleteItemAsync(GetCommand(), _user.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Ending_A_Session_Of_An_Admin_Account_As_An_App_Admin_Is_Not_Found_And_Ends_Nothing()
    {
        // Arrange
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:admin"]);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler()
                .DeleteItemAsync(
                    GetCommand() with
                    {
                        Changer = new UserChanger("admin-id", false),
                    },
                    "admin-id",
                    TestContext.Current.CancellationToken
                )
        );

        A.CallTo(() =>
                _refreshTokenRepository.DeleteEntityItemsAsync(A<string>._, A<int?>._, A<Guid?>._, A<Guid?>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }

    private EndSessionCommand GetCommand() => new(_user.Id, _sessionId, _registeredApp, new UserChanger(_user.Id, false));

    private EndSessionCommandHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _userManager,
            _userRegisteredAppRepository,
            _refreshTokenRepository
        );
}
