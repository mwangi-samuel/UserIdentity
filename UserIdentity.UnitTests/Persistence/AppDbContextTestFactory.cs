using Microsoft.EntityFrameworkCore;
using UserIdentity.Persistence;

namespace UserIdentity.UnitTests.Persistence;

internal static class AppDbContextTestFactory
{
    public static AppDbContext GetAppDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

        return new AppDbContext(options);
    }
}
