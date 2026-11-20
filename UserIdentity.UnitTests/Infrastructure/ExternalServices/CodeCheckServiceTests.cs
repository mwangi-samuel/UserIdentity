using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FakeItEasy;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Settings;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.ExternalServices;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Infrastructure.ExternalServices;
using Xunit;

namespace UserIdentity.UnitTests.Infrastructure.ExternalServices;

public sealed class CodeCheckServiceTests : IDisposable
{
    private const string VerifyUrl = "http://code-service.test/api/v1/message/verify";
    private const string ResendUrl = "http://code-service.test/api/v1/message/retry";

    private static readonly string[] _serviceRoles = [KnownRoles.InternalService];
    private static readonly string[] _serviceScopes = ["otherservice-message:create"];

    private readonly IHttpRequestSender _httpRequestSender = A.Fake<IHttpRequestSender>();
    private readonly IJwtTokenHandler _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
    private readonly IRequestIdAccessor _requestIdAccessor = A.Fake<IRequestIdAccessor>();
    private readonly HttpRequestMessage _requestMessage = new();

    public CodeCheckServiceTests()
    {
        A.CallTo(() => _httpRequestSender.CreateHttpRequestMessage(HttpMethod.Post, A<string>._)).Returns(_requestMessage);
        A.CallTo(() => _jwtTokenHandler.GenerateScopeClaim("otherservice-message", ScopeActions.Create))
            .Returns(new Claim("scope", "otherservice-message:create"));
        A.CallTo(() =>
                _jwtTokenHandler.CreateToken(
                    A<string>._,
                    A<string>._,
                    A<string>._,
                    A<HashSet<string>>._,
                    A<HashSet<string>>._,
                    A<HashSet<Claim>?>._
                )
            )
            .Returns(("service-token", 180));
        A.CallTo(() => _requestIdAccessor.RequestId).Returns("request-id");
    }

    public void Dispose() => _requestMessage.Dispose();

    [Theory]
    [InlineData("Verified", CodeCheckOutcome.Verified)]
    [InlineData("WrongCode", CodeCheckOutcome.WrongCode)]
    [InlineData("Expired", CodeCheckOutcome.Expired)]
    [InlineData("NoAttemptsLeft", CodeCheckOutcome.NoAttemptsLeft)]
    [InlineData("NotFound", CodeCheckOutcome.NotFound)]
    public async Task Check_Posts_The_Code_With_A_Service_Token_And_Reads_The_Outcome(string answer, CodeCheckOutcome expected)
    {
        // Arrange
        SetUpAnswer(answer);

        // Act
        var outcome = await GetService().CheckAsync("testapp", "LoginCode", "ticket-id", "123456", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, outcome);
        A.CallTo(() => _httpRequestSender.CreateHttpRequestMessage(HttpMethod.Post, VerifyUrl)).MustHaveHappenedOnceExactly();
        Assert.Equal("Bearer", _requestMessage.Headers.Authorization?.Scheme);
        Assert.Equal("service-token", _requestMessage.Headers.Authorization?.Parameter);

        using var body = JsonDocument.Parse(await _requestMessage.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("LoginCode", body.RootElement.GetProperty("action").GetString());
        Assert.Equal("ticket-id", body.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("123456", body.RootElement.GetProperty("code").GetString());

        A.CallTo(() =>
                _jwtTokenHandler.CreateToken(
                    "useridentity",
                    "useridentity",
                    "testapp",
                    A<HashSet<string>>.That.IsSameSequenceAs(_serviceRoles),
                    A<HashSet<string>>.That.IsSameSequenceAs(_serviceScopes),
                    A<HashSet<Claim>?>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("Sent", CodeResendOutcome.Sent)]
    [InlineData("TooSoon", CodeResendOutcome.TooSoon)]
    [InlineData("NoResendsLeft", CodeResendOutcome.NoResendsLeft)]
    [InlineData("NotFound", CodeResendOutcome.NotFound)]
    public async Task Resend_Posts_To_The_Resend_Address_And_Reads_The_Outcome(string answer, CodeResendOutcome expected)
    {
        // Arrange
        SetUpAnswer(answer);

        // Act
        var outcome = await GetService().ResendAsync("testapp", "WelcomeUser", "user-id", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, outcome);
        A.CallTo(() => _httpRequestSender.CreateHttpRequestMessage(HttpMethod.Post, ResendUrl)).MustHaveHappenedOnceExactly();

        using var body = JsonDocument.Parse(await _requestMessage.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("WelcomeUser", body.RootElement.GetProperty("action").GetString());
        Assert.Equal("user-id", body.RootElement.GetProperty("correlationId").GetString());
        Assert.False(body.RootElement.TryGetProperty("code", out _));
    }

    [Theory]
    [InlineData("Sent")]
    [InlineData("verified")]
    [InlineData("1")]
    [InlineData(null)]
    public async Task An_Unknown_Or_Missing_Outcome_Throws(string? answer)
    {
        // Arrange
        SetUpAnswer(answer);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            GetService().CheckAsync("testapp", "LoginCode", "ticket-id", "123456", TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task An_Empty_Answer_Throws()
    {
        // Arrange
        A.CallTo(() =>
                _httpRequestSender.SendRequestAsync<CodeCheckService.CodeCheckResponse>(
                    CodeCheckService.TargetServiceName,
                    A<HttpRequestMessage>._,
                    HttpStatusCode.OK,
                    A<CancellationToken>._
                )
            )
            .Returns((CodeCheckService.CodeCheckResponse)null!);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            GetService().ResendAsync("testapp", "LoginCode", "ticket-id", TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task A_Failed_Request_Throws()
    {
        // Arrange
        A.CallTo(() =>
                _httpRequestSender.SendRequestAsync<CodeCheckService.CodeCheckResponse>(
                    A<string>._,
                    A<HttpRequestMessage>._,
                    A<HttpStatusCode>._,
                    A<CancellationToken>._
                )
            )
            .ThrowsAsync(new HttpRequestException("Request failed with status code: ServiceUnavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            GetService().CheckAsync("testapp", "LoginCode", "ticket-id", "123456", TestContext.Current.CancellationToken)
        );
    }

    private void SetUpAnswer(string? outcome) =>
        A.CallTo(() =>
                _httpRequestSender.SendRequestAsync<CodeCheckService.CodeCheckResponse>(
                    CodeCheckService.TargetServiceName,
                    A<HttpRequestMessage>._,
                    HttpStatusCode.OK,
                    A<CancellationToken>._
                )
            )
            .Returns(new CodeCheckService.CodeCheckResponse(outcome));

    private CodeCheckService GetService() =>
        new(
            _httpRequestSender,
            _jwtTokenHandler,
            _requestIdAccessor,
            Options.Create(new RoleSettings { ServiceName = "UserIdentity" }),
            Options.Create(
                new CodeCheckSettings
                {
                    VerifyUrl = VerifyUrl,
                    ResendUrl = ResendUrl,
                    ServiceName = "otherservice",
                }
            )
        );
}
