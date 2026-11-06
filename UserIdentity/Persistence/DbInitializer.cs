using Microsoft.EntityFrameworkCore;

namespace UserIdentity.Persistence;

public static class DbInitializer
{
    public static void InitializeDb(IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        var database = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>().Database;

        if (database.IsRelational())
            database.Migrate();
        else
            database.EnsureCreated();
    }
}
