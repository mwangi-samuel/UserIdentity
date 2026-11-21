using System.Security.Authentication;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class ConfirmAccountCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly ILoginTicketRepository _loginTicketRepository = A.Fake<ILoginTicketRepository>();
    private readonly ICodeCheckService _codeCheckService = A.Fake<ICodeCheckService>();
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = A.Fake<
        ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel>
    >();
    private readonly FakeTimeProvider _timeProvider = new(_now);

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new() { Id = "user-1", UserName = "test" };

    private LoginTicketEntity? _loginTicket;

    public ConfirmAccountCommandHandlerTests()
    {
        A.CallTo(() => _tokenFactory.HashToken("ticket")).Returns("ticket-hash");
        A.CallTo(() =>
                _loginTicketRepository.GetEntityByAlternateIdAsync(
                    A<LoginTicketEntity>.That.Matches(e => e.Ticket == "ticket-hash" && e.AppId == _registeredApp.Id),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .ReturnsLazily(() => _loginTicket);
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(CodeCheckOutcome.Verified);
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new AuthUserViewModel { User = new UserDto(), NextStep = SignInStep.Session });
    }

    [Theory]
    [InlineData(UserMessages.WelcomeUserAction, "user-1")]
    [InlineData(UserMessages.LoginCodeAction, "ticket-id")]
    public async Task Confirm_With_The_Right_Code_Deletes_The_Ticket_And_Signs_In_With_The_Email_Proved(
        string codeAction,
        string correlationId
    )
    {
        // Arrange
        _loginTicket = GetTicket(codeAction, correlationId);

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.Session, vm.NextStep);
        A.CallTo(() => _codeCheckService.CheckAsync(_registeredApp.AppName, codeAction, correlationId, "123456", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _loginTicketRepository.RemoveEntityItem(_loginTicket)).MustHaveHappenedOnceExactly())
            .Then(
                A.CallTo(() =>
                        _signInUserCommandHandler.CreateItemAsync(
                            A<SignInUserCommand>.That.Matches(c => c.User == _user && c.EmailProven && !c.ChangingMethod),
                            _user.Id,
                            A<CancellationToken>._
                        )
                    )
                    .MustHaveHappenedOnceExactly()
            )
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task Confirm_From_A_Method_Change_Continues_To_Set_Up()
    {
        // Arrange
        _loginTicket = GetTicket(UserMessages.LoginCodeAction, "ticket-id");
        _loginTicket.ChangingMethod = true;

        // Act
        await GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _signInUserCommandHandler.CreateItemAsync(
                    A<SignInUserCommand>.That.Matches(c => c.EmailProven && c.ChangingMethod),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(CodeCheckOutcome.WrongCode)]
    [InlineData(CodeCheckOutcome.Expired)]
    [InlineData(CodeCheckOutcome.NoAttemptsLeft)]
    [InlineData(CodeCheckOutcome.NotFound)]
    public async Task Confirm_With_A_Code_That_Fails_Gives_The_One_Failed_Code_Message(CodeCheckOutcome outcome)
    {
        // Arrange
        _loginTicket = GetTicket(UserMessages.WelcomeUserAction, _user.Id);
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(outcome);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        A.CallTo(() => _loginTicketRepository.RemoveEntityItem(A<LoginTicketEntity>._)).MustNotHaveHappened();
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("other-step")]
    public async Task Confirm_With_An_Unknown_Ticket_Still_Checks_A_Code_Then_Gives_The_Same_Message(string ticketState)
    {
        // Arrange
        _loginTicket = ticketState switch
        {
            "missing" => null,
            "expired" => GetTicket(UserMessages.WelcomeUserAction, _user.Id, expires: _now),
            _ => GetTicket(UserMessages.LoginCodeAction, "ticket-id", step: SignInStep.SecondFactor),
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        A.CallTo(() =>
                _codeCheckService.CheckAsync(
                    _registeredApp.AppName,
                    UserMessages.WelcomeUserAction,
                    A<string>.That.Not.IsEqualTo(_user.Id),
                    "123456",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Confirm_For_A_Locked_Out_User_Throws_InvalidCredentialException_Without_Checking_The_Code()
    {
        // Arrange
        _loginTicket = GetTicket(UserMessages.WelcomeUserAction, _user.Id);
        A.CallTo(() => _userManager.IsLockedOutAsync(_user)).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private LoginTicketEntity GetTicket(
        string codeAction,
        string correlationId,
        DateTime? expires = null,
        SignInStep step = SignInStep.ConfirmEmail
    ) =>
        new()
        {
            Ticket = "ticket-hash",
            UserId = _user.Id,
            AppId = _registeredApp.Id,
            Expires = expires ?? _now.AddMinutes(10),
            Step = step,
            CodeAction = codeAction,
            CodeCorrelationId = correlationId,
        };

    private ConfirmAccountCommand GetCommand() =>
        new()
        {
            Ticket = "ticket",
            Code = "123456",
            RegisteredApp = _registeredApp,
        };

    private ConfirmAccountCommandHandler GetHandler() =>
        new(_tokenFactory, _userManager, _unitOfWork, _loginTicketRepository, _codeCheckService, _signInUserCommandHandler, _timeProvider);
}
