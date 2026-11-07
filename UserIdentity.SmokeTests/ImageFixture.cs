using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Testcontainers.MySql;
using Testcontainers.Nats;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.SmokeTests;

// Runs the published image, not the build the other test projects use, so what trimming removed is really gone
public sealed class ImageFixture : IAsyncLifetime
{
    public const string ApiKey = "SmokeApiKey-For-Tests-Only-0123456789012";
    public const string UserUpdatedSubject = "useridentity.user.updated";

    private const string CodeVerifiedSubject = "messagemanager.user.updated";
    private const string ImageVariable = "USERIDENTITY_SMOKE_IMAGE";
    private const string LocalImage = "useridentity:smoke";
    private const string DatabaseName = "useridentity";
    private const string DatabaseUser = "useridentity";
    private const string DatabasePassword = "useridentity-smoke";
    private const string NatsUser = "useridentity";
    private const string NatsPassword = "useridentity-smoke";
    private const string MysqlAlias = "db";
    private const string NatsAlias = "nats";
    private const string UserIdentityStream = "USERIDENTITY";
    private const ushort ServicePort = 8080;
    private const ushort NatsPort = 4222;

    // The limits docker-compose.yml gives the service
    private const long MemoryLimitBytes = 256L * 1024 * 1024;
    private const long NanoCpuLimit = 500_000_000;

    private static readonly TimeSpan _startupTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan _messageWait = TimeSpan.FromSeconds(30);

    private static readonly string[] _errorMarkers = ["\"@l\":\"Error\"", "\"@l\":\"Fatal\"", "Unhandled exception"];

    private readonly INetwork _network = new NetworkBuilder().Build();
    private readonly MySqlContainer _mysql;
    private readonly NatsContainer _nats;

    private IContainer? _service;
    private NatsConnection? _natsConnection;
    private NatsJSContext? _jetStream;

    public ImageFixture()
    {
        _mysql = new MySqlBuilder("mysql:8.4.0")
            .WithUsername(DatabaseUser)
            .WithPassword(DatabasePassword)
            .WithDatabase(DatabaseName)
            .WithNetwork(_network)
            .WithNetworkAliases(MysqlAlias)
            .Build();

        _nats = new NatsBuilder("nats:2.15.2-alpine")
            .WithUsername(NatsUser)
            .WithPassword(NatsPassword)
            .WithCommand("--jetstream")
            .WithNetwork(_network)
            .WithNetworkAliases(NatsAlias)
            .Build();
    }

    public Uri ServiceUrl => new($"http://{_service!.Hostname}:{_service.GetMappedPublicPort(ServicePort)}");

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var image = await ResolveImageAsync(cancellationToken);

        await _network.CreateAsync(cancellationToken);
        await Task.WhenAll(_mysql.StartAsync(cancellationToken), _nats.StartAsync(cancellationToken));

        _natsConnection = new NatsConnection(
            NatsOpts.Default with
            {
                Url = $"nats://{_nats.Hostname}:{_nats.GetMappedPublicPort(NatsPort)}",
                AuthOpts = NatsAuthOpts.Default with { Username = NatsUser, Password = NatsPassword },
            }
        );
        _jetStream = new NatsJSContext(_natsConnection);

        // The sending service owns this stream. Without it, the service's consumer backs off for up to 30 seconds
        await _jetStream.CreateOrUpdateStreamAsync(new StreamConfig("MESSAGEMANAGER", ["messagemanager.>"]), cancellationToken);

        _service = BuildService(image);

        await _service.StartAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var errors = await ReadErrorLinesAsync();

        if (_natsConnection is not null)
            await _natsConnection.DisposeAsync();

        if (_service is not null)
            await _service.DisposeAsync();

        await _nats.DisposeAsync();
        await _mysql.DisposeAsync();
        await _network.DisposeAsync();

        if (errors.Count > 0)
            Assert.Fail(string.Join('\n', errors));
    }

    public async Task<JsonNode> FindMessageAsync(string subject, string correlationId, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_messageWait);

        var consumer = await CreateOrderedConsumerAsync(subject, timeout.Token);

        await foreach (var delivery in consumer.ConsumeAsync<byte[]>(cancellationToken: timeout.Token))
        {
            var message = JsonNode.Parse(delivery.Data);

            if ($"{message?["correlationId"]}" == correlationId)
                return message!;
        }

        throw new InvalidOperationException($"No message on {subject} for {correlationId}.");
    }

    public async Task PublishCodeVerifiedAsync(string correlationId, string action, CancellationToken cancellationToken)
    {
        var message = new JsonObject
        {
            ["eventType"] = "Update",
            ["correlationId"] = correlationId,
            ["registeredApp"] = ServiceClient.AppName,
            ["action"] = action,
            ["attributes"] = new JsonObject(),
            ["occurredAt"] = DateTime.UtcNow,
        };

        var ack = await _jetStream!.PublishAsync(
            CodeVerifiedSubject,
            Encoding.UTF8.GetBytes(message.ToJsonString()),
            cancellationToken: cancellationToken
        );

        ack.EnsureSuccess();
    }

    public async Task GrantRoleAsync(string userName, string roleName, CancellationToken cancellationToken)
    {
        var script = await File.ReadAllTextAsync(Path.Combine(FindRepositoryRoot(), ".sql", "add_admin_user_role.sql"), cancellationToken);

        await RunSqlAsync($"SET @username = '{userName}', @role_name = '{roleName}';\n{script}", cancellationToken);
    }

    public async Task<string> RunSqlAsync(string sql, CancellationToken cancellationToken)
    {
        var result = await _mysql.ExecScriptAsync(sql, cancellationToken);

        return result.ExitCode == 0 ? result.Stdout.Trim() : throw new InvalidOperationException($"SQL failed: {result.Stderr}");
    }

    private IContainer BuildService(string image)
    {
        var privateKeyPassPhrase = Guid.NewGuid().ToString("N");
        var keyPair = EdDsaHelper.GenerateEd25519KeyPair();

        return new ContainerBuilder(image)
            .WithNetwork(_network)
            .WithPortBinding(ServicePort, true)
            .WithEnvironment("MysqlSettings__Host", MysqlAlias)
            .WithEnvironment("MysqlSettings__Database", DatabaseName)
            .WithEnvironment("MysqlSettings__UserName", DatabaseUser)
            .WithEnvironment("MysqlSettings__Password", DatabasePassword)
            .WithEnvironment("MessagingSettings__NatsUrl", $"nats://{NatsAlias}:{NatsPort}")
            .WithEnvironment("MessagingSettings__NatsUser", NatsUser)
            .WithEnvironment("MessagingSettings__NatsPassword", NatsPassword)
            .WithEnvironment("EDDSA_PRIVATE_KEY", EdDsaHelper.ConvertToPem(keyPair.Private, privateKeyPassPhrase))
            .WithEnvironment("EDDSA_PUBLIC_KEY", EdDsaHelper.ConvertToPem(keyPair.Public))
            .WithEnvironment("KeySetOptions__PrivateKeyPassPhrase", privateKeyPassPhrase)
            .WithEnvironment("ApiKeySettings__ApiKey", ApiKey)
            .WithEnvironment("RateLimitSettings__PermitLimit", "100000")
            .WithEnvironment("CleanupSettings__IntervalInMinutes", "1")
            .WithCreateParameterModifier(parameters =>
            {
                parameters.HostConfig!.Memory = MemoryLimitBytes;
                parameters.HostConfig!.NanoCPUs = NanoCpuLimit;
            })
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(
                        request => request.ForPort(ServicePort).ForPath("/health"),
                        strategy => strategy.WithTimeout(_startupTimeout)
                    )
            )
            .Build();
    }

    // Testcontainers builds with the classic builder, which doesn't set the Dockerfile's $BUILDPLATFORM
    private static async Task<string> ResolveImageAsync(CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable(ImageVariable) is { Length: > 0 } image)
            return image;

        ProcessStartInfo startInfo = new("docker", ["build", "-f", "UserIdentity/Dockerfile", "-t", LocalImage, "."])
        {
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var build = Process.Start(startInfo)!;

        var output = build.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = build.StandardError.ReadToEndAsync(cancellationToken);

        await build.WaitForExitAsync(cancellationToken);

        return build.ExitCode == 0
            ? LocalImage
            : throw new InvalidOperationException($"docker build failed:\n{await output}\n{await errors}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UserIdentity.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("UserIdentity.slnx wasn't found.");
    }

    // The service creates its stream when it starts sending, so wait for it
    private async Task<INatsJSConsumer> CreateOrderedConsumerAsync(string subject, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                return await _jetStream!.CreateOrderedConsumerAsync(
                    UserIdentityStream,
                    new NatsJSOrderedConsumerOpts { FilterSubjects = [subject] },
                    cancellationToken
                );
            }
            catch (NatsJSApiException ex) when (ex.Error.Code == 404)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
        }
    }

    private async Task<List<string>> ReadErrorLinesAsync()
    {
        if (_service is not { State: TestcontainersStates.Running })
            return [];

        var (stdout, stderr) = await _service.GetLogsAsync();

        return [.. $"{stdout}\n{stderr}".Split('\n').Where(line => _errorMarkers.Any(line.Contains))];
    }
}
