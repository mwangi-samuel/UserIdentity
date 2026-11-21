using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.Tokens.Queries;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Queries;

public class GetSessionsQueryHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

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

    public GetSessionsQueryHandlerTests()
    {
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:user"]);
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(true);
    }

    [Fact]
    public async Task Sessions_Are_The_Users_Live_Refresh_Tokens_In_The_App_With_The_Callers_Marked_Current()
    {
        // Arrange
        var current = new RefreshTokenEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = _user.Id,
            AppId = _registeredApp.Id,
            Expires = _now.AddDays(30),
            IpAddress = "203.0.113.7",
            Device = "Test Browser",
            CreatedAt = _now.AddDays(-2),
            UpdatedAt = _now.AddMinutes(-5),
        };
        var neverRefreshed = new RefreshTokenEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = _user.Id,
            AppId = _registeredApp.Id,
            Expires = _now.AddDays(29),
            CreatedAt = _now.AddDays(-1),
        };

        A.CallTo(() => _refreshTokenRepository.GetEntityItemsAsync(_user.Id, _registeredApp.Id, _now, A<CancellationToken>._))
            .Returns([current, neverRefreshed]);

        // Act
        var vm = await GetHandler().GetItemsAsync(GetQuery(current.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                new SessionDto
                {
                    Id = current.Id,
                    SignedInAt = _now.AddDays(-2),
                    LastActiveAt = _now.AddMinutes(-5),
                    ExpiresAt = _now.AddDays(30),
                    IpAddress = "203.0.113.7",
                    Device = "Test Browser",
                    Current = true,
                },
                new SessionDto
                {
                    Id = neverRefreshed.Id,
                    SignedInAt = _now.AddDays(-1),
                    LastActiveAt = _now.AddDays(-1),
                    ExpiresAt = _now.AddDays(29),
                    Current = false,
                },
            ],
            vm.Sessions
        );
    }

    [Fact]
    public async Task Without_A_Current_Session_No_Session_Is_Current()
    {
        // Arrange
        A.CallTo(() => _refreshTokenRepository.GetEntityItemsAsync(_user.Id, _registeredApp.Id, _now, A<CancellationToken>._))
            .Returns([
                new RefreshTokenEntity
                {
                    Id = Guid.CreateVersion7(),
                    UserId = _user.Id,
                    AppId = _registeredApp.Id,
                    Expires = _now.AddDays(30),
                },
            ]);

        // Act
        var vm = await GetHandler().GetItemsAsync(GetQuery(null), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Assert.Single(vm.Sessions).Current);
    }

    [Fact]
    public async Task Sessions_Of_Another_Apps_User_Are_Not_Found()
    {
        // Arrange
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._))
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler().GetItemsAsync(GetQuery(null), TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _refreshTokenRepository.GetEntityItemsAsync(A<string>._, A<int>._, A<DateTime>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private GetSessionsQuery GetQuery(Guid? currentSessionId) =>
        new()
        {
            UserId = _user.Id,
            RegisteredApp = _registeredApp,
            Changer = new UserChanger(_user.Id, false),
            CurrentSessionId = currentSessionId,
        };

    private GetSessionsQueryHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _userManager,
            _userRegisteredAppRepository,
            _refreshTokenRepository,
            new FakeTimeProvider(_now)
        );
}
