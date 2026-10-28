using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.Security.KeySets;

using UserIdentity.Persistence;

using Xunit;

namespace UserIdentity.IntegrationTests;

public class InviteOpenApiContractTests
{
  [Fact]
  public async Task OpenApi_DescribesAppScopedInvitationSearchAndStatusFilters()
  {
    // Arrange
    await using var factory = new MemoryFactory();
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    // Act
    var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
    var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    using var document = System.Text.Json.JsonDocument.Parse(json);
    var parameters = document.RootElement.GetProperty("paths").GetProperty("/api/v1/invite-code").GetProperty("get").GetProperty("parameters").EnumerateArray();
    Assert.Contains(parameters, parameter => parameter.GetProperty("name").GetString() == "search");
    Assert.Contains(document.RootElement.GetProperty("paths").GetProperty("/api/v1/invite-code").GetProperty("get").GetProperty("parameters").EnumerateArray(), parameter => parameter.GetProperty("name").GetString() == "applied");
    if (Environment.GetEnvironmentVariable("IDENTITY_OPENAPI_PATH") is { } target)
    {
      await File.WriteAllTextAsync(target, json, TestContext.Current.CancellationToken);
    }
  }

  private sealed class MemoryFactory : WebApplicationFactory<Program>
  {
    private readonly string _database = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      builder.UseEnvironment("Development");
      var keys = UserIdentity.IntegrationTests.TestUtils.EdDsaHelper.GenerateEd25519KeyPair();
      var phrase = StringUtil.GenerateRandomString(32, true, true);
      builder.UseSetting("EDDSA_PRIVATE_KEY", UserIdentity.IntegrationTests.TestUtils.EdDsaHelper.ConvertToPem(keys.Private, phrase));
      builder.UseSetting("EDDSA_PUBLIC_KEY", UserIdentity.IntegrationTests.TestUtils.EdDsaHelper.ConvertToPem(keys.Public));
      builder.UseSetting($"{nameof(KeySetOptions)}:{nameof(KeySetOptions.PrivateKeyPath)}", "EDDSA_PRIVATE_KEY");
      builder.UseSetting($"{nameof(KeySetOptions)}:{nameof(KeySetOptions.PrivateKeyPassPhrase)}", phrase);
      builder.ConfigureTestServices(services =>
      {
        services.RemoveAll<DbContext>();
        services.RemoveAll<AppDbContext>();
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
        services.RemoveAll<IHostedService>();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_database));
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<AppDbContext>());
      });
    }
  }
}
