using System.Text.Json;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Utilities;
using Xunit;

namespace UserIdentity.IntegrationTests.TestUtils;

public sealed class NatsTestHelper(INatsJSContext jetStream)
{
    public const string UserIdentityStream = "USERIDENTITY";
    public const string UserRegisteredSubject = "useridentity.user.registered";
    public const string PasswordResetRequestedSubject = "useridentity.password-reset.requested";
    public const string LoginCodeRequestedSubject = "useridentity.login-code.requested";
    public const string PasswordChangedSubject = "useridentity.password.changed";
    public const string SignUpAttemptedSubject = "useridentity.sign-up.attempted";
    public const string TwoFactorChangedSubject = "useridentity.two-factor.changed";
    public const string ContactChangedSubject = "useridentity.contact.changed";

    private static readonly TimeSpan _waitTime = TimeSpan.FromSeconds(10);

    private readonly INatsJSContext _jetStream = jetStream;

    public static async Task CreateStreamAsync(INatsJSContext jetStream, CancellationToken cancellationToken) =>
        await jetStream.CreateOrUpdateStreamAsync(new StreamConfig(UserIdentityStream, ["useridentity.>"]), cancellationToken);

    // Other tests' messages share the stream, so the message is found by its correlation id
    public Task<MessageEvent?> FindMessageAsync(string subject, string correlationId, TimeSpan? waitTime = null) =>
        FindMessageAsync(subject, message => message.CorrelationId == correlationId, waitTime);

    public async Task<MessageEvent?> FindMessageAsync(string subject, Func<MessageEvent, bool> matches, TimeSpan? waitTime = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(waitTime ?? _waitTime);

        try
        {
            var consumer = await CreateOrderedConsumerAsync(subject, timeout.Token);

            await foreach (var delivery in consumer.ConsumeAsync<byte[]>(cancellationToken: timeout.Token))
            {
                var message = JsonSerializer.Deserialize<MessageEvent>(delivery.Data, JsonOptionsCache.Get());

                if (message is not null && matches(message))
                    return message;
            }
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested) { }

        return null;
    }

    // The service creates its stream when it starts sending, so wait for it
    private async Task<INatsJSConsumer> CreateOrderedConsumerAsync(string subject, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                return await _jetStream.CreateOrderedConsumerAsync(
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
}
