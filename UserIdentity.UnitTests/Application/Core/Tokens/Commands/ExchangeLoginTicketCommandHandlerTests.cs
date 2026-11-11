using System.Security.Authentication;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class ExchangeLoginTicketCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly IOptions<IdentityOptions> _identityOptions;
    private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly ILoginTicketRepository _loginTicketRepository = A.Fake<ILoginTicketRepository>();
    private readonly ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> _startSessionCommandHandler = A.Fake<
        ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel>
    >();
    private readonly FakeTimeProvider _timeProvider = new();

    private readonly ActiveRegisteredApp _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        AppSecretKey = "test-secret",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new()
    {
        Id = "user-1",
        UserName = "test",
        Email = "test@example.com",
        EmailConfirmed = true,
    };

    private readonly LoginTicketEntity _loginTicket;

    public ExchangeLoginTicketCommandHandlerTests()
    {
        var identityOptions = new IdentityOptions();
        identityOptions.SignIn.RequireConfirmedAccount = true;
        _identityOptions = Options.Create(identityOptions);

        _timeProvider.SetUtcNow(Now);

        _loginTicket = new LoginTicketEntity
        {
            Id = Guid.NewGuid(),
            Ticket = "ticket-hash",
            UserId = _user.Id,
            AppId = _registeredApp.Id,
            Expires = Now.AddMinutes(30),
        };

        A.CallTo(() => _tokenFactory.HashToken("ticket")).Returns("ticket-hash");
        A.CallTo(() =>
                _loginTicketRepository.GetEntityByAlternateIdAsync(
                    A<LoginTicketEntity>.That.Matches(e => e.Ticket == "ticket-hash" && e.AppId == _registeredApp.Id),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(_loginTicket);
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, _registeredApp.AppName, A<CancellationToken>._))
            .Returns(new UserDto { Id = _user.Id, UserName = _user.UserName });
    }

    [Fact]
    public async Task Exchange_Of_A_Ticket_For_A_Confirmed_User_Uses_The_Ticket_And_Starts_A_Session()
    {
        // Arrange
        var session = new UserTokenViewModel
        {
            UserToken = new UserTokenDto
            {
                AccessToken = new AccessTokenDto { Token = "access-token", ExpiresIn = 180 },
                RefreshToken = "refresh-token",
            },
        };

        A.CallTo(() =>
                _startSessionCommandHandler.CreateItemAsync(
                    new StartSessionCommand(_user.Id, _user.UserName!, _registeredApp.AppName),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .Returns(session);

        var handler = GetHandler();

        // Act
        var vm = await handler.CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(vm.IsConfirmed);
        Assert.Equal(_user.Id, vm.User.Id);
        Assert.Same(session.UserToken, vm.UserToken);
        Assert.Null(vm.LoginTicket);

        A.CallTo(() => _loginTicketRepository.RemoveEntityItem(_loginTicket))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task Exchange_Of_An_Unknown_Ticket_Throws_InvalidCredentialException()
    {
        // Arrange
        A.CallTo(() => _tokenFactory.HashToken("other-ticket")).Returns("other-hash");
        A.CallTo(() =>
                _loginTicketRepository.GetEntityByAlternateIdAsync(
                    A<LoginTicketEntity>.That.Matches(e => e.Ticket == "other-hash"),
                    A<QueryCondition>._,
                    A<CancellationToken>._
                )
            )
            .Returns((LoginTicketEntity?)null);

        var handler = GetHandler();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            handler.CreateItemAsync(GetCommand("other-ticket"), string.Empty, TestContext.Current.CancellationToken)
        );

        AssertNoSessionStarted();
    }

    [Fact]
    public async Task Exchange_Of_An_Expired_Ticket_Throws_InvalidCredentialException()
    {
        // Arrange
        _loginTicket.Expires = Now;

        var handler = GetHandler();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            handler.CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        AssertNoSessionStarted();
    }

    [Fact]
    public async Task Exchange_Before_The_User_Is_Confirmed_Throws_IllegalOperationException_And_Keeps_The_Ticket()
    {
        // Arrange
        _user.EmailConfirmed = false;

        var handler = GetHandler();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            handler.CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.AccountNotConfirmed, exception.Message);
        AssertNoSessionStarted();
    }

    [Fact]
    public async Task Exchange_For_A_Locked_Out_User_Throws_InvalidCredentialException()
    {
        // Arrange
        A.CallTo(() => _userManager.IsLockedOutAsync(_user)).Returns(true);

        var handler = GetHandler();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            handler.CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        AssertNoSessionStarted();
    }

    [Fact]
    public async Task Exchange_For_A_User_Without_A_Profile_For_The_App_Throws_InvalidCredentialException()
    {
        // Arrange
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, _registeredApp.AppName, A<CancellationToken>._)).Returns((UserDto?)null);

        var handler = GetHandler();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            handler.CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        AssertNoSessionStarted();
    }

    private void AssertNoSessionStarted()
    {
        A.CallTo(() => _loginTicketRepository.RemoveEntityItem(A<LoginTicketEntity>._)).MustNotHaveHappened();
        A.CallTo(() => _startSessionCommandHandler.CreateItemAsync(A<StartSessionCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    private ExchangeLoginTicketCommand GetCommand(string ticket = "ticket") => new() { Ticket = ticket, RegisteredApp = _registeredApp };

    private ExchangeLoginTicketCommandHandler GetHandler() =>
        new(
            _identityOptions,
            _tokenFactory,
            _userManager,
            _unitOfWork,
            _userRepository,
            _loginTicketRepository,
            _startSessionCommandHandler,
            _timeProvider
        );
}
