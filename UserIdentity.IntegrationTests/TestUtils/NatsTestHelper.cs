using System.Reflection;
using System.Text.Json;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.Messaging.Receiving;
using UserIdentity.Infrastructure.Messaging;
using Xunit;

namespace UserIdentity.IntegrationTests.TestUtils;

public sealed class NatsTestHelper(INatsJSContext jetStream)
{
    public const string UserIdentityStream = "USERIDENTITY";
    public const string UserRegisteredSubject = "useridentity.user.registered";
    public const string PasswordResetRequestedSubject = "useridentity.password-reset.requested";
    public const string CallbackRequestedSubject = "useridentity.callback.requested";
    public static readonly string CodeVerifiedSubject = typeof(CodeVerifiedMessageHandler)
        .GetCustomAttribute<MessageSubjectAttribute>()!
        .Subject;

    private static readonly string _codeVerifiedSender = CodeVerifiedSubject.Split('.')[0];
    private static readonly string _codeVerifiedConsumer = $"useridentity_{CodeVerifiedSubject.Replace('.', '_')}";

    private static readonly TimeSpan _waitTime = TimeSpan.FromSeconds(10);

    private readonly INatsJSContext _jetStream = jetStream;

    public static async Task CreateStreamsAsync(INatsJSContext jetStream, CancellationToken cancellationToken)
    {
        await jetStream.CreateOrUpdateStreamAsync(
            new StreamConfig(_codeVerifiedSender.ToUpperInvariant(), [$"{_codeVerifiedSender}.>"]),
            cancellationToken
        );
        await jetStream.CreateOrUpdateStreamAsync(new StreamConfig(UserIdentityStream, ["useridentity.>"]), cancellationToken);
    }

    // Other tests' messages share the stream, so the message is found by its correlation id
    public async Task<MessageEvent?> FindMessageAsync(string subject, string correlationId, TimeSpan? waitTime = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(waitTime ?? _waitTime);

        try
        {
            var consumer = await CreateOrderedConsumerAsync(subject, timeout.Token);

            await foreach (var delivery in consumer.ConsumeAsync<byte[]>(cancellationToken: timeout.Token))
            {
                var message = JsonSerializer.Deserialize<MessageEvent>(delivery.Data, JsonOptionsCache.Get());

                if (message?.CorrelationId == correlationId)
                    return message;
            }
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested) { }

        return null;
    }

    public async Task<ulong> PublishCodeVerifiedAsync(MessageEvent message)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptionsCache.Get());

        var ack = await _jetStream.PublishAsync(CodeVerifiedSubject, payload, cancellationToken: TestContext.Current.CancellationToken);

        ack.EnsureSuccess();

        return ack.Seq;
    }

    public async Task<bool> IsCodeVerifiedAcknowledgedAsync(ulong sequence)
    {
        var consumer = await _jetStream.GetConsumerAsync(
            _codeVerifiedSender.ToUpperInvariant(),
            _codeVerifiedConsumer,
            TestContext.Current.CancellationToken
        );

        return consumer.Info.AckFloor.StreamSeq >= sequence && consumer.Info.NumAckPending == 0 && consumer.Info.NumRedelivered == 0;
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
