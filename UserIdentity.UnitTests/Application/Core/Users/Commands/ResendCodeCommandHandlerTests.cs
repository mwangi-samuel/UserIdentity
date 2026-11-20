using FakeItEasy;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.PasswordResets;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class ResendCodeCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
    private readonly ILoginTicketRepository _loginTicketRepository = A.Fake<ILoginTicketRepository>();
    private readonly IPasswordResetRepository _passwordResetRepository = A.Fake<IPasswordResetRepository>();
    private readonly ICodeCheckService _codeCheckService = A.Fake<ICodeCheckService>();
    private readonly FakeTimeProvider _timeProvider = new(_now);

    private readonly ActiveRegisteredApp _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    public ResendCodeCommandHandlerTests()
    {
        A.CallTo(() => _tokenFactory.HashToken(A<string>._)).ReturnsLazily((string value) => $"{value}-hash");
    }

    [Theory]
    [InlineData(SignInStep.ConfirmEmail, UserMessages.WelcomeUserAction, "user-1")]
    [InlineData(SignInStep.SecondFactor, UserMessages.LoginCodeAction, "ticket-id")]
    [InlineData(SignInStep.SetUpSecondFactor, UserMessages.LoginCodeAction, "ticket-id")]
    public async Task Resend_With_A_Ticket_Resends_The_Code_The_Ticket_Waits_For(SignInStep step, string codeAction, string correlationId)
    {
        // Arrange
        SetUpTicket(
            new LoginTicketEntity
            {
                Ticket = "ticket-hash",
                AppId = _registeredApp.Id,
                Step = step,
                CodeAction = codeAction,
                CodeCorrelationId = correlationId,
                Expires = _now.AddMinutes(5),
            }
        );

        // Act
        await GetHandler()
            .CreateItemAsync(
                new ResendCodeCommand { Ticket = "ticket", RegisteredApp = _registeredApp },
                string.Empty,
                TestContext.Current.CancellationToken
            );

        // Assert
        A.CallTo(() => _codeCheckService.ResendAsync(_registeredApp.AppName, codeAction, correlationId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Resend_With_A_Reset_Request_Resends_The_Reset_Code()
    {
        // Arrange
        var resetId = Guid.NewGuid();
        SetUpReset(
            new PasswordResetEntity
            {
                Id = resetId,
                RequestId = "reset-hash",
                AppId = _registeredApp.Id,
                Expires = _now.AddMinutes(5),
            }
        );

        // Act
        await GetHandler()
            .CreateItemAsync(
                new ResendCodeCommand { ResetRequestId = "reset", RegisteredApp = _registeredApp },
                string.Empty,
                TestContext.Current.CancellationToken
            );

        // Assert
        A.CallTo(() =>
                _codeCheckService.ResendAsync(
                    _registeredApp.AppName,
                    UserMessages.PasswordResetAction,
                    resetId.ToString(),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(CodeResendOutcome.TooSoon)]
    [InlineData(CodeResendOutcome.NoResendsLeft)]
    [InlineData(CodeResendOutcome.NotFound)]
    public async Task Resend_Answers_The_Same_Whatever_The_Code_Service_Says(CodeResendOutcome outcome)
    {
        // Arrange
        SetUpTicket(
            new LoginTicketEntity
            {
                Ticket = "ticket-hash",
                AppId = _registeredApp.Id,
                Step = SignInStep.ConfirmEmail,
                CodeAction = UserMessages.WelcomeUserAction,
                CodeCorrelationId = "user-1",
                Expires = _now.AddMinutes(5),
            }
        );
        A.CallTo(() => _codeCheckService.ResendAsync(A<string>._, A<string>._, A<string>._, A<CancellationToken>._)).Returns(outcome);

        // Act
        var vm = await GetHandler()
            .CreateItemAsync(
                new ResendCodeCommand { Ticket = "ticket", RegisteredApp = _registeredApp },
                string.Empty,
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.NotNull(vm);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("no-code")]
    public async Task Resend_With_An_Unknown_Ticket_Still_Calls_The_Code_Service_With_A_Random_Code(string ticketState)
    {
        // Arrange
        SetUpTicket(
            ticketState switch
            {
                "missing" => null,
                "expired" => new LoginTicketEntity
                {
                    Ticket = "ticket-hash",
                    CodeAction = UserMessages.WelcomeUserAction,
                    CodeCorrelationId = "user-1",
                    Expires = _now,
                },
                _ => new LoginTicketEntity
                {
                    Ticket = "ticket-hash",
                    Step = SignInStep.SecondFactor,
                    Expires = _now.AddMinutes(5),
                },
            }
        );

        // Act
        await GetHandler()
            .CreateItemAsync(
                new ResendCodeCommand { Ticket = "ticket", RegisteredApp = _registeredApp },
                string.Empty,
                TestContext.Current.CancellationToken
            );

        // Assert
        A.CallTo(() =>
                _codeCheckService.ResendAsync(
                    _registeredApp.AppName,
                    UserMessages.WelcomeUserAction,
                    A<string>.That.Not.IsEqualTo("user-1"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Resend_For_An_Applied_Reset_Calls_The_Code_Service_With_A_Random_Code()
    {
        // Arrange
        var resetId = Guid.NewGuid();
        SetUpReset(
            new PasswordResetEntity
            {
                Id = resetId,
                RequestId = "reset-hash",
                Applied = true,
                Expires = _now.AddMinutes(5),
            }
        );

        // Act
        await GetHandler()
            .CreateItemAsync(
                new ResendCodeCommand { ResetRequestId = "reset", RegisteredApp = _registeredApp },
                string.Empty,
                TestContext.Current.CancellationToken
            );

        // Assert
        A.CallTo(() =>
                _codeCheckService.ResendAsync(
                    _registeredApp.AppName,
                    UserMessages.PasswordResetAction,
                    A<string>.That.Not.IsEqualTo(resetId.ToString()),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    private void SetUpTicket(LoginTicketEntity? loginTicket) =>
        A.CallTo(() =>
                _loginTicketRepository.GetEntityByAlternateIdAsync(
                    A<LoginTicketEntity>.That.Matches(e => e.Ticket == "ticket-hash"),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(loginTicket);

    private void SetUpReset(PasswordResetEntity passwordReset) =>
        A.CallTo(() =>
                _passwordResetRepository.GetEntityByAlternateIdAsync(
                    A<PasswordResetEntity>.That.Matches(e => e.RequestId == "reset-hash"),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(passwordReset);

    private ResendCodeCommandHandler GetHandler() =>
        new(_tokenFactory, _loginTicketRepository, _passwordResetRepository, _codeCheckService, _timeProvider);
}
