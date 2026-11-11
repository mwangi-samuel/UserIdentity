using Xunit;

namespace UserIdentity.IntegrationTests.TestUtils;

internal static class Eventually
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    public static Task HoldsAsync(Func<bool> condition) => HoldsAsync(() => Task.FromResult(condition()));

    public static async Task HoldsAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.Add(_timeout);

        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"The condition didn't hold within {_timeout.TotalSeconds} seconds");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }
}
