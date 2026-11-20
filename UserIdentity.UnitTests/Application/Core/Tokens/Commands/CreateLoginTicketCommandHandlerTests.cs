using FakeItEasy;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Interfaces;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Persistence.Repositories.LoginTickets;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class CreateLoginTicketCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly ILoginTicketRepository _repository = A.Fake<ILoginTicketRepository>();
    private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();

    private LoginTicketEntity? _stored;

    public CreateLoginTicketCommandHandlerTests()
    {
        A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns("login-ticket");
        A.CallTo(() => _tokenFactory.HashToken("login-ticket")).Returns("login-ticket-hash");
        A.CallTo(() => _repository.CreateEntityItem(A<LoginTicketEntity>._)).Invokes((LoginTicketEntity entity) => _stored = entity);
    }

    [Fact]
    public async Task A_Sign_Up_Ticket_Keeps_The_Sign_Up_Lifetime_And_Stores_Only_The_Hash()
    {
        // Act
        var vm = await GetHandler()
            .CreateItemAsync(
                new CreateLoginTicketCommand
                {
                    UserId = "user-id",
                    AppId = 7,
                    Step = SignInStep.ConfirmEmail,
                    CodeAction = UserMessages.WelcomeUserAction,
                    CodeCorrelationId = "user-id",
                },
                "user-id",
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.Equal("login-ticket", vm.Ticket);
        Assert.Equal(_now.AddSeconds(3600), vm.Expires);
        Assert.NotNull(_stored);
        Assert.Equal("login-ticket-hash", _stored.Ticket);
        Assert.Equal("user-id", _stored.UserId);
        Assert.Equal(7, _stored.AppId);
        Assert.Equal(SignInStep.ConfirmEmail, _stored.Step);
        Assert.Equal(UserMessages.WelcomeUserAction, _stored.CodeAction);
        Assert.Equal("user-id", _stored.CodeCorrelationId);
        Assert.Equal(_now.AddSeconds(3600), _stored.Expires);
        Assert.Equal("user-id", _stored.CreatedBy);
    }

    [Fact]
    public async Task Every_Other_Ticket_Lasts_The_Two_Factor_Ticket_Lifetime_And_Keeps_The_Given_Id()
    {
        // Arrange
        var id = Guid.CreateVersion7();

        // Act
        var vm = await GetHandler()
            .CreateItemAsync(
                new CreateLoginTicketCommand
                {
                    Id = id,
                    UserId = "user-id",
                    AppId = 7,
                    Step = SignInStep.SecondFactor,
                    CodeAction = UserMessages.LoginCodeAction,
                    CodeCorrelationId = id.ToString(),
                    EmailProven = true,
                },
                "user-id",
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.Equal(_now.AddSeconds(600), vm.Expires);
        Assert.NotNull(_stored);
        Assert.Equal(id, _stored.Id);
        Assert.True(_stored.EmailProven);
        Assert.False(_stored.ChangingMethod);
        Assert.Equal(SignInStep.SecondFactor, _stored.Step);
    }

    private CreateLoginTicketCommandHandler GetHandler() =>
        new(
            Options.Create(new LoginTicketSettings { ValidForSeconds = 3600 }),
            Options.Create(new TwoFactorSettings { TicketLifetimeSeconds = 600 }),
            _repository,
            _tokenFactory,
            new FakeTimeProvider(_now)
        );
}
