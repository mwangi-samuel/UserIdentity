using FakeItEasy;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using UserIdentity.Application.Core.AccountLimits.Commands;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.Users;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class SendLoginCodeCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly ICreateItemCommandHandler<
        CreateAccountLimitEventCommand,
        AccountLimitViewModel
    > _createAccountLimitEventCommandHandler = A.Fake<ICreateItemCommandHandler<CreateAccountLimitEventCommand, AccountLimitViewModel>>();
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        A.Fake<ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel>>();

    [Fact]
    public async Task Within_The_Daily_Cap_The_Code_Is_Published_And_Its_Address_Masked()
    {
        // Arrange
        SetUpCap(isReached: false);

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(vm.Sent);
        Assert.Equal("t***@example.com", vm.CodeSentTo);
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.LoginCodeRequested
                        && c.Message.Action == UserMessages.LoginCodeAction
                        && c.Message.CorrelationId == "ticket-id"
                        && c.Message.ExpiresAt == _now.AddMinutes(10)
                        && c.Message.Recipient!.Email == "test@example.com"
                        && c.Message.Recipient.Phone == null
                        && c.Message.GetData<UserMessageData>()!.Purpose == LoginCodePurpose.SignIn
                        && c.Message.GetData<UserMessageData>()!.RequestedAt == _now
                    ),
                    "user-1",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Past_The_Daily_Cap_Nothing_Is_Published()
    {
        // Arrange
        SetUpCap(isReached: true);

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(vm.Sent);
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }

    private void SetUpCap(bool isReached) =>
        A.CallTo(() =>
                _createAccountLimitEventCommandHandler.CreateItemAsync(
                    new CreateAccountLimitEventCommand("user-1", AccountLimitKind.LoginCodeSent),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new AccountLimitViewModel { IsReached = isReached });

    private static SendLoginCodeCommand GetCommand() =>
        new()
        {
            UserId = "user-1",
            UserDetails = new UserEntity { Id = "user-1", FirstName = "Test" },
            AppName = "TestApp",
            Purpose = LoginCodePurpose.SignIn,
            CorrelationId = "ticket-id",
            ExpiresAt = _now.AddMinutes(10),
            Email = "test@example.com",
            RequestContext = new RequestContext(),
        };

    private SendLoginCodeCommandHandler GetHandler() =>
        new(_createAccountLimitEventCommandHandler, _createOutboxMessageCommandHandler, new FakeTimeProvider(_now));
}
