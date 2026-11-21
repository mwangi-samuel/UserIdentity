using System.Security.Authentication;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.Queries;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Queries;

public class GetSecondStepTicketQueryHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly ILoginTicketRepository _loginTicketRepository = A.Fake<ILoginTicketRepository>();
    private readonly IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> _getAccountLimitQueryHandler = A.Fake<
        IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel>
    >();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new() { Id = "user-1" };

    private readonly LoginTicketEntity _loginTicket = new()
    {
        Ticket = "ticket-hash",
        UserId = "user-1",
        AppId = 1,
        Step = SignInStep.SecondFactor,
        Expires = _now.AddMinutes(5),
    };

    public GetSecondStepTicketQueryHandlerTests()
    {
        A.CallTo(() => _tokenFactory.HashToken("ticket")).Returns("ticket-hash");
        A.CallTo(() =>
                _loginTicketRepository.GetEntityByAlternateIdAsync(
                    A<LoginTicketEntity>.That.Matches(e => e.Ticket == "ticket-hash" && e.AppId == 1),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(_loginTicket);
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _getAccountLimitQueryHandler.GetItemAsync(A<GetAccountLimitQuery>._, A<CancellationToken>._))
            .Returns(new AccountLimitViewModel { IsReached = false });
    }

    [Fact]
    public async Task A_Usable_Ticket_Gives_Its_User()
    {
        // Act
        var vm = await GetHandler().GetItemAsync(GetQuery(SignInStep.SecondFactor), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(_loginTicket, vm.LoginTicket);
        Assert.Same(_user, vm.User);
    }

    [Theory]
    [InlineData("other-step")]
    [InlineData("expired")]
    public async Task A_Ticket_For_Another_Step_Or_Expired_Gives_The_One_Failed_Code_Message(string ticketState)
    {
        // Arrange
        if (ticketState == "expired")
            _loginTicket.Expires = _now;

        var step = ticketState == "other-step" ? SignInStep.SetUpSecondFactor : SignInStep.SecondFactor;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().GetItemAsync(GetQuery(step), TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
    }

    [Fact]
    public async Task A_Locked_Out_User_Gets_The_Lockout()
    {
        // Arrange
        A.CallTo(() => _userManager.IsLockedOutAsync(_user)).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            GetHandler().GetItemAsync(GetQuery(SignInStep.SecondFactor), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Past_The_Failed_Second_Step_Cap_The_User_Gets_The_Lockout()
    {
        // Arrange
        A.CallTo(() =>
                _getAccountLimitQueryHandler.GetItemAsync(
                    new GetAccountLimitQuery(_user.Id, AccountLimitKind.FailedSecondStep),
                    A<CancellationToken>._
                )
            )
            .Returns(new AccountLimitViewModel { IsReached = true });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            GetHandler().GetItemAsync(GetQuery(SignInStep.SecondFactor), TestContext.Current.CancellationToken)
        );
    }

    private GetSecondStepTicketQuery GetQuery(SignInStep step) => new("ticket", _registeredApp, step);

    private GetSecondStepTicketQueryHandler GetHandler() =>
        new(_tokenFactory, _userManager, _loginTicketRepository, _getAccountLimitQueryHandler, new FakeTimeProvider(_now));
}
