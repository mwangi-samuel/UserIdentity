using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Commands;

public class ResetUserTwoFactorCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
    private readonly ILoginTicketRepository _loginTicketRepository = A.Fake<ILoginTicketRepository>();
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        A.Fake<ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel>>();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new() { Id = "user-1", Email = "user@test.com" };
    private readonly UserEntity _userDetails = new()
    {
        Id = "user-1",
        FirstName = "Test",
        TwoFactorMethod = TwoFactorMethod.Sms,
    };

    public ResetUserTwoFactorCommandHandlerTests()
    {
        A.CallTo(_userManager).WithReturnType<Task<IdentityResult>>().Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.FindByEmailAsync(_user.Email!)).Returns(_user);
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:user"]);
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _userRepository.GetEntityItemAsync(_user.Id, A<CancellationToken>._)).Returns(_userDetails);
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(new UserDto { Id = _user.Id });
    }

    [Fact]
    public async Task Reset_Removes_The_Method_Ends_The_Sessions_And_Notifies_Without_Saying_Who()
    {
        // Act
        var vm = await GetHandler().UpdateItemAsync(GetCommand(isGlobalAdmin: false), "admin-id", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_user.Id, vm.User.Id);
        Assert.Null(_userDetails.TwoFactorMethod);
        A.CallTo(() => _userManager.SetTwoFactorEnabledAsync(_user, false)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _refreshTokenRepository.DeleteEntityItemsAsync(_user.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _loginTicketRepository.DeleteEntityItemsAsync(_user.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.TwoFactorChanged
                        && c.Message.GetData<UserMessageData>()!.Change == nameof(TwoFactorChange.Reset)
                        && c.Message.GetData<UserMessageData>()!.Method == TwoFactorMethod.Sms
                        && c.Message.GetData<UserMessageData>()!.IpAddress == null
                    ),
                    "admin-id",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Only_A_Global_Admin_Can_Reset_An_Admin_Account(bool isGlobalAdmin, bool refused)
    {
        // Arrange
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:admin"]);

        // Act
        Task<UserIdentity.Application.Core.Users.ViewModels.UserViewModel> reset() =>
            GetHandler().UpdateItemAsync(GetCommand(isGlobalAdmin), "admin-id", TestContext.Current.CancellationToken);

        // Assert
        if (refused)
            await Assert.ThrowsAsync<NoRecordException>((Func<Task<UserIdentity.Application.Core.Users.ViewModels.UserViewModel>>)reset);
        else
            await reset();

        A.CallTo(() => _userManager.SetTwoFactorEnabledAsync(A<IdentityUser>._, A<bool>._))
            .MustHaveHappened(refused ? 0 : 1, Times.Exactly);
    }

    [Fact]
    public async Task An_Unknown_Email_Is_Not_Found()
    {
        // Arrange
        A.CallTo(() => _userManager.FindByEmailAsync(_user.Email!)).Returns((IdentityUser?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler().UpdateItemAsync(GetCommand(isGlobalAdmin: false), "admin-id", TestContext.Current.CancellationToken)
        );
    }

    private ResetUserTwoFactorCommand GetCommand(bool isGlobalAdmin) =>
        new()
        {
            Email = _user.Email!,
            RegisteredApp = _registeredApp,
            Changer = new UserChanger("admin-id", isGlobalAdmin),
        };

    private ResetUserTwoFactorCommandHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _userManager,
            _unitOfWork,
            _userRepository,
            _userRegisteredAppRepository,
            _refreshTokenRepository,
            _loginTicketRepository,
            _createOutboxMessageCommandHandler,
            new FakeTimeProvider(_now)
        );
}
