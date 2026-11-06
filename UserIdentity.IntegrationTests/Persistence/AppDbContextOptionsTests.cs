using EFCore.NamingConventions.Internal;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PolyzenKit.Infrastructure.Configurations;
using PolyzenKit.Persistence.Infrastructure;
using PolyzenKit.Persistence.Settings;
using UserIdentity.Persistence;
using Xunit;

namespace UserIdentity.IntegrationTests.Persistence;

public class AppDbContextOptionsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    [Fact]
    public void UseAppMysql_Adds_The_Unique_Violation_Interceptor_And_Snake_Case_Names()
    {
        // Arrange
        var mysqlSettings = Configuration.GetRequiredSetting<MysqlSettings>();
        var builder = new DbContextOptionsBuilder<AppDbContext>();

        // Act
        builder.UseAppMysql(mysqlSettings);

        // Assert
        var interceptors = builder.Options.FindExtension<CoreOptionsExtension>()?.Interceptors;
        Assert.NotNull(interceptors);
        Assert.Single(interceptors.OfType<UniqueViolationInterceptor>());
        Assert.NotNull(builder.Options.FindExtension<NamingConventionsOptionsExtension>());

        using var context = new AppDbContext(builder.Options);
        var normalizedEmail = context.Model.FindEntityType(typeof(IdentityUser))?.FindProperty(nameof(IdentityUser.NormalizedEmail));
        Assert.Equal("normalized_email", normalizedEmail?.GetColumnName());
    }
}
