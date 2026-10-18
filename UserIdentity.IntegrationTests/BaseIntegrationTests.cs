using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Polly;
using Polly.Retry;

using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Core.RegisteredApps.Settings;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.AppEntities;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Persistence.Infrastructure;
using PolyzenKit.Presentation.Settings;

using Respawn;

using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;

using Xunit;

namespace UserIdentity.IntegrationTests;

[Collection("Integration Tests")]
public class BaseIntegrationTests : IAsyncLifetime
{
  private readonly IServiceScope _scope;
  private readonly Func<Task> _resetDatabase;

  protected readonly ITestOutputHelper _outputHelper;
  protected readonly HttpClient _httpClient;
  protected readonly IServiceProvider _serviceProvider;

  private readonly AppDbContext _appDbContext;

  protected readonly TestDbHelper _testDbHelper;
  protected readonly KafkaConsumerHelper _kafkaConsumerHelper;
  protected readonly KafkaProducerHelper _kafkaProducerHelper;

  protected readonly RegisteredAppEntity _registeredApp;
  protected readonly ApiKeySettings _apiKeySettings;

  protected readonly IConfiguration _configuration;
  protected readonly ResiliencePipeline _pollyResiliencePipeline;

  public BaseIntegrationTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
  {
    _outputHelper = outputHelper;
    _httpClient = testingWebAppFactory.CreateClient();

    _scope = testingWebAppFactory.Services.CreateScope();
    _serviceProvider = _scope.ServiceProvider;

    _appDbContext = _serviceProvider.GetRequiredService<AppDbContext>();

    _configuration = _serviceProvider.GetRequiredService<IConfiguration>();

    _testDbHelper = new TestDbHelper(
      _configuration,
      _appDbContext,
      _serviceProvider.GetRequiredService<UserManager<IdentityUser>>(),
      _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>()
    );

    _kafkaConsumerHelper = new KafkaConsumerHelper(_configuration);
    _kafkaProducerHelper = new KafkaProducerHelper(
      _configuration,
      _serviceProvider.GetRequiredService<IKafkaMessageProducer<string, MessageEvent>>()
      );

    _registeredApp = _configuration.GetSetting<RegisteredAppsSettings>().RegisteredApps.FirstOrDefault()
      ?? throw new MissingConfigurationException("At least one registered app must be provided");

    _apiKeySettings = _configuration.GetSetting<ApiKeySettings>();

    _pollyResiliencePipeline = new ResiliencePipelineBuilder()
      .AddRetry(new RetryStrategyOptions
      {
        MaxRetryAttempts = 5,
        OnRetry = args =>
        {
          outputHelper.WriteLine("OnRetry, Attempt: {0}", args.AttemptNumber);
          return default;
        }
      })
      .AddTimeout(TimeSpan.FromSeconds(2))
      .Build();

    _resetDatabase = ResolveResetDatabaseFunc();
  }

  private Func<Task> ResolveResetDatabaseFunc()
  {
    return async () =>
    {
      var dbConnection = _appDbContext.Database.GetDbConnection();

      await dbConnection.OpenAsync();

      var respawner = await Respawner.CreateAsync(dbConnection, new RespawnerOptions
      {
        DbAdapter = DbAdapter.MySql,
        TablesToIgnore = [
           _appDbContext.GetTableName<AppEntity>()!,
           _appDbContext.GetTableName<RegisteredAppEntity>()!,
           _appDbContext.GetTableName<IdentityRole>()!,
           HistoryRepository.DefaultTableName
        ]
      });

      await respawner.ResetAsync(dbConnection);
    };
  }

  public async ValueTask InitializeAsync() => await _resetDatabase();

  public ValueTask DisposeAsync()
  {
    _httpClient.Dispose();
    _scope.Dispose();
    GC.SuppressFinalize(this);
    return ValueTask.CompletedTask;
  }
}
