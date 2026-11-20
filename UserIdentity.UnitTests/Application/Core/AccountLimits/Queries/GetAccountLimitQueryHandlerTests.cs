using FakeItEasy;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using UserIdentity.Application.Core.AccountLimits.Queries;
using UserIdentity.Application.Core.AccountLimits.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Persistence.Repositories.AccountLimitEvents;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.AccountLimits.Queries;

public class GetAccountLimitQueryHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly IAccountLimitEventRepository _repository = A.Fake<IAccountLimitEventRepository>();

    [Theory]
    [InlineData(AccountLimitKind.FailedSecondStep, 9, false)]
    [InlineData(AccountLimitKind.FailedSecondStep, 10, true)]
    [InlineData(AccountLimitKind.LoginCodeSent, 9, false)]
    [InlineData(AccountLimitKind.LoginCodeSent, 10, true)]
    [InlineData(AccountLimitKind.SignUpAttemptNotice, 4, false)]
    [InlineData(AccountLimitKind.SignUpAttemptNotice, 5, true)]
    public async Task The_Daily_Caps_Count_The_Last_Day_Of_Events(AccountLimitKind kind, int count, bool expected)
    {
        // Arrange
        A.CallTo(() => _repository.GetHistoryAsync("user-1", kind, _now.AddDays(-1), A<CancellationToken>._))
            .Returns((count, _now.AddHours(-2)));

        // Act
        var vm = await GetHandler().GetItemAsync(new GetAccountLimitQuery("user-1", kind), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, vm.IsReached);
    }

    [Theory]
    [InlineData(59, true)]
    [InlineData(61, false)]
    public async Task Sign_Up_Notices_Wait_For_The_Cool_Down(int secondsAgo, bool expected)
    {
        // Arrange
        A.CallTo(() => _repository.GetHistoryAsync("user-1", AccountLimitKind.SignUpAttemptNotice, A<DateTime>._, A<CancellationToken>._))
            .Returns((1, _now.AddSeconds(-secondsAgo)));

        // Act
        var vm = await GetHandler()
            .GetItemAsync(new GetAccountLimitQuery("user-1", AccountLimitKind.SignUpAttemptNotice), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, vm.IsReached);
    }

    private GetAccountLimitQueryHandler GetHandler() =>
        new(Options.Create(new AccountLimitSettings()), _repository, new FakeTimeProvider(_now));
}
