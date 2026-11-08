using FakeItEasy;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Interfaces;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Persistence.Repositories.LoginTickets;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class CreateLoginTicketCommandHandlerTests
{
    [Fact]
    public async Task Create_Login_Ticket_Adds_A_Hashed_Ticket_For_The_User_And_App_That_Expires_After_The_Configured_Time()
    {
        // Arrange
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(now);

        var repository = A.Fake<ILoginTicketRepository>();
        var tokenFactory = A.Fake<ITokenFactory>();

        A.CallTo(() => tokenFactory.GenerateToken(32)).Returns("login-ticket");
        A.CallTo(() => tokenFactory.HashToken("login-ticket")).Returns("login-ticket-hash");

        LoginTicketEntity? stored = null;
        A.CallTo(() => repository.CreateEntityItem(A<LoginTicketEntity>._)).Invokes((LoginTicketEntity entity) => stored = entity);

        var handler = new CreateLoginTicketCommandHandler(
            Options.Create(new LoginTicketSettings { ValidForSeconds = 3600 }),
            repository,
            tokenFactory,
            timeProvider
        );

        // Act
        var vm = await handler.CreateItemAsync(
            new CreateLoginTicketCommand("user-id", 7),
            "user-id",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("login-ticket", vm.Ticket);

        Assert.NotNull(stored);
        Assert.Equal("login-ticket-hash", stored.Ticket);
        Assert.Equal("user-id", stored.UserId);
        Assert.Equal(7, stored.AppId);
        Assert.Equal(now.AddSeconds(3600), stored.Expires);
        Assert.Equal("user-id", stored.CreatedBy);
    }
}
