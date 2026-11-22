using FakeItEasy;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Common.Paging;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Roles;
using UserIdentity.Persistence.Repositories.Users;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Queries;

public class GetUsersQueryHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IRoleRepository _roleRepository = A.Fake<IRoleRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly UserDto _user = new()
    {
        Id = "user-1",
        UserName = "test.user",
        Email = "user@test.com",
        EmailConfirmed = true,
        PhoneNumber = "0712345678",
        FirstName = "Test",
        LastName = "User",
        TwoFactorMethod = TwoFactorMethod.Email,
        CreatedAt = _now.AddDays(-3),
    };

    private readonly UserDto _admin = new() { Id = "admin-2", CreatedAt = _now.AddDays(-2) };

    private readonly UserDto _globalAdmin = new() { Id = "global-admin-2", CreatedAt = _now.AddDays(-1) };

    private readonly UserDto _caller = new() { Id = "admin-id", CreatedAt = _now.AddDays(-4) };

    public GetUsersQueryHandlerTests()
    {
        A.CallTo(() =>
                _userRepository.SearchUserDtosByAppIdAsync(
                    A<int>._,
                    A<string?>._,
                    A<string?>._,
                    A<bool?>._,
                    A<int>._,
                    A<int>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new PagedList<UserDto>([_user, _admin, _globalAdmin, _caller], 4));
        A.CallTo(() => _roleRepository.GetRoleNamesByUserIdAsync(A<IReadOnlyCollection<string>>._, A<CancellationToken>._))
            .Returns(
                new Dictionary<string, List<string>>
                {
                    [_user.Id] = ["otherapp:owner", "testapp:user"],
                    [_admin.Id] = ["testapp:admin"],
                    [_globalAdmin.Id] = ["global:admin", "otherapp:admin"],
                    [_caller.Id] = ["testapp:admin"],
                }
            );
        A.CallTo(() =>
                _refreshTokenRepository.GetLastActiveAtByUserIdAsync(A<int>._, A<IReadOnlyCollection<string>>._, A<CancellationToken>._)
            )
            .Returns(new Dictionary<string, DateTime?> { [_user.Id] = _now.AddHours(-1) });
    }

    [Fact]
    public async Task The_Filters_Are_Trimmed_And_Passed_With_The_Page_For_The_Apps_Users()
    {
        // Act
        var vm = await GetHandler()
            .GetItemsAsync(
                GetQuery(search: "  test  ", role: " testapp:user ", emailConfirmed: true, page: 2, pageSize: 10),
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.Equal(4, vm.TotalCount);
        Assert.Equal([_user.Id, _admin.Id, _globalAdmin.Id, _caller.Id], vm.Users.Select(user => user.Id));
        A.CallTo(() => _userRepository.SearchUserDtosByAppIdAsync(1, "test", "testapp:user", true, 2, 10, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Blank_Filters_Are_Left_Out()
    {
        // Act
        await GetHandler().GetItemsAsync(GetQuery(search: " ", role: " "), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userRepository.SearchUserDtosByAppIdAsync(1, null, null, null, 1, 50, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("otherapp:user")]
    [InlineData("global:admin")]
    [InlineData("user")]
    public async Task A_Role_Outside_The_App_Is_Refused_Before_Searching(string role)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().GetItemsAsync(GetQuery(role: role), TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Roles.NameOutsideApp("testapp:"), exception.Message);
        A.CallTo(_userRepository).MustNotHaveHappened();
    }

    [Fact]
    public async Task Each_User_Carries_The_Apps_Roles_Last_Activity_And_Lockout()
    {
        // Arrange
        A.CallTo(() =>
                _userRepository.SearchUserDtosByAppIdAsync(
                    A<int>._,
                    A<string?>._,
                    A<string?>._,
                    A<bool?>._,
                    A<int>._,
                    A<int>._,
                    A<CancellationToken>._
                )
            )
            .Returns(
                new PagedList<UserDto>(
                    [
                        new UserDto
                        {
                            Id = _user.Id,
                            UserName = _user.UserName,
                            Email = _user.Email,
                            EmailConfirmed = true,
                            PhoneNumber = _user.PhoneNumber,
                            FirstName = _user.FirstName,
                            LastName = _user.LastName,
                            TwoFactorMethod = _user.TwoFactorMethod,
                            CreatedAt = _user.CreatedAt,
                            LockoutEnd = new DateTimeOffset(_now.AddMinutes(3)),
                        },
                        _globalAdmin,
                    ],
                    2
                )
            );

        // Act
        var vm = await GetHandler().GetItemsAsync(GetQuery(), TestContext.Current.CancellationToken);

        // Assert
        var user = vm.Users[0];
        var globalAdmin = vm.Users[1];

        Assert.Equal("test.user", user.UserName);
        Assert.Equal("Test", user.FirstName);
        Assert.Equal("User", user.LastName);
        Assert.Equal("user@test.com", user.Email);
        Assert.True(user.EmailConfirmed);
        Assert.Equal("0712345678", user.PhoneNumber);
        Assert.Equal(TwoFactorMethod.Email, user.TwoFactorMethod);
        Assert.Equal(_now.AddDays(-3), user.CreatedAt);
        Assert.Equal(["testapp:user"], user.Roles);
        Assert.Equal(_now.AddHours(-1), user.LastActiveAt);
        Assert.True(user.LockedOut);
        Assert.Equal(["global:admin"], globalAdmin.Roles);
        Assert.Null(globalAdmin.LastActiveAt);
        Assert.False(globalAdmin.LockedOut);
        A.CallTo(() =>
                _roleRepository.GetRoleNamesByUserIdAsync(
                    A<IReadOnlyCollection<string>>.That.IsSameSequenceAs(new[] { _user.Id, _globalAdmin.Id }),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _refreshTokenRepository.GetLastActiveAtByUserIdAsync(
                    1,
                    A<IReadOnlyCollection<string>>.That.IsSameSequenceAs(new[] { _user.Id, _globalAdmin.Id }),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task A_Lockout_That_Has_Passed_Isnt_Shown()
    {
        // Arrange
        A.CallTo(() =>
                _userRepository.SearchUserDtosByAppIdAsync(
                    A<int>._,
                    A<string?>._,
                    A<string?>._,
                    A<bool?>._,
                    A<int>._,
                    A<int>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new PagedList<UserDto>([new UserDto { Id = _user.Id, LockoutEnd = new DateTimeOffset(_now) }], 1));

        // Act
        var vm = await GetHandler().GetItemsAsync(GetQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Assert.Single(vm.Users).LockedOut);
    }

    [Fact]
    public async Task An_App_Admin_May_Delete_Only_Other_Accounts_Without_An_Admin_Role()
    {
        // Act
        var vm = await GetHandler().GetItemsAsync(GetQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [(_user.Id, true), (_admin.Id, false), (_globalAdmin.Id, false), (_caller.Id, false)],
            vm.Users.Select(user => (user.Id, user.DeleteEnabled))
        );
    }

    [Fact]
    public async Task A_Global_Admin_May_Delete_Every_Other_Account()
    {
        // Act
        var vm = await GetHandler()
            .GetItemsAsync(GetQuery(changer: new UserChanger(_caller.Id, true)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [(_user.Id, true), (_admin.Id, true), (_globalAdmin.Id, true), (_caller.Id, false)],
            vm.Users.Select(user => (user.Id, user.DeleteEnabled))
        );
    }

    [Fact]
    public async Task An_Empty_Page_Reads_No_Roles_Or_Sessions()
    {
        // Arrange
        A.CallTo(() =>
                _userRepository.SearchUserDtosByAppIdAsync(
                    A<int>._,
                    A<string?>._,
                    A<string?>._,
                    A<bool?>._,
                    A<int>._,
                    A<int>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new PagedList<UserDto>([], 12));

        // Act
        var vm = await GetHandler().GetItemsAsync(GetQuery(page: 9), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(vm.Users);
        Assert.Equal(12, vm.TotalCount);
        A.CallTo(_roleRepository).MustNotHaveHappened();
        A.CallTo(_refreshTokenRepository).MustNotHaveHappened();
    }

    private GetUsersQuery GetQuery(
        string? search = null,
        string? role = null,
        bool? emailConfirmed = null,
        int page = 1,
        int pageSize = 50,
        UserChanger? changer = null
    ) =>
        new()
        {
            RegisteredApp = _registeredApp,
            Changer = changer ?? new UserChanger(_caller.Id, false),
            Search = search,
            Role = role,
            EmailConfirmed = emailConfirmed,
            Page = page,
            PageSize = pageSize,
        };

    private GetUsersQueryHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity", GlobalAdminRole = "global:admin" }),
            _userRepository,
            _roleRepository,
            _refreshTokenRepository,
            new FakeTimeProvider(_now)
        );
}
