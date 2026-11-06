using Xunit;

namespace UserIdentity.IntegrationTests.TestUtils;

internal static class Eventually
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    public static async Task HoldsAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.Add(_timeout);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"The condition didn't hold within {_timeout.TotalSeconds} seconds");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }
}
