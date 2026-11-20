using FakeItEasy;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Application.Core.AccountLimits.Commands;
using UserIdentity.Application.Core.AccountLimits.Queries;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.AccountLimitEvents;
using UserIdentity.Persistence.Repositories.AccountLimitEvents;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.AccountLimits.Commands;

public class CreateAccountLimitEventCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly IAccountLimitEventRepository _repository = A.Fake<IAccountLimitEventRepository>();
    private readonly IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> _getAccountLimitQueryHandler = A.Fake<
        IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel>
    >();

    [Fact]
    public async Task Within_The_Cap_The_Event_Is_Recorded()
    {
        // Arrange
        A.CallTo(() => _getAccountLimitQueryHandler.GetItemAsync(A<GetAccountLimitQuery>._, A<CancellationToken>._))
            .Returns(new AccountLimitViewModel { IsReached = false });

        // Act
        var vm = await GetHandler()
            .CreateItemAsync(
                new CreateAccountLimitEventCommand("user-1", AccountLimitKind.LoginCodeSent),
                "user-1",
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.False(vm.IsReached);
        A.CallTo(() =>
                _repository.CreateEntityItem(
                    A<AccountLimitEventEntity>.That.Matches(e =>
                        e.UserId == "user-1" && e.Kind == AccountLimitKind.LoginCodeSent && e.CreatedAt == _now
                    )
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task At_The_Cap_Nothing_Is_Recorded()
    {
        // Arrange
        A.CallTo(() => _getAccountLimitQueryHandler.GetItemAsync(A<GetAccountLimitQuery>._, A<CancellationToken>._))
            .Returns(new AccountLimitViewModel { IsReached = true });

        // Act
        var vm = await GetHandler()
            .CreateItemAsync(
                new CreateAccountLimitEventCommand("user-1", AccountLimitKind.LoginCodeSent),
                "user-1",
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.True(vm.IsReached);
        A.CallTo(() => _repository.CreateEntityItem(A<AccountLimitEventEntity>._)).MustNotHaveHappened();
    }

    private CreateAccountLimitEventCommandHandler GetHandler() =>
        new(_repository, _getAccountLimitQueryHandler, new FakeTimeProvider(_now));
}
