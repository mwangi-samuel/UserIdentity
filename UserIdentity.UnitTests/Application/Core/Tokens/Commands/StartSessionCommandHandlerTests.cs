using FakeItEasy;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class StartSessionCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);

    private readonly ICreateItemCommandHandler<CreateAccessTokenCommand, AccessTokenViewModel> _createAccessTokenCommandHandler = A.Fake<
        ICreateItemCommandHandler<CreateAccessTokenCommand, AccessTokenViewModel>
    >();
    private readonly IRefreshTokenRepository _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
    private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
    private readonly FakeTimeProvider _timeProvider = new();

    public StartSessionCommandHandlerTests() => _timeProvider.SetUtcNow(_now);

    [Fact]
    public async Task Start_Session_Adds_A_Hashed_Refresh_Token_That_Expires_After_The_Configured_Time()
    {
        // Arrange
        A.CallTo(() =>
                _createAccessTokenCommandHandler.CreateItemAsync(
                    new CreateAccessTokenCommand("user-id", "test.user", "TestApp"),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .Returns(
                new AccessTokenViewModel
                {
                    AccessToken = new AccessTokenDto { Token = "access-token", ExpiresIn = 180 },
                }
            );
        A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns("refresh-token");
        A.CallTo(() => _tokenFactory.HashToken("refresh-token")).Returns("refresh-token-hash");

        RefreshTokenEntity? stored = null;
        A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>._))
            .Invokes((RefreshTokenEntity entity) => stored = entity);

        var handler = new StartSessionCommandHandler(
            Options.Create(new RefreshTokenSettings { ValidForSeconds = 3600 }),
            _createAccessTokenCommandHandler,
            _refreshTokenRepository,
            _tokenFactory,
            _timeProvider
        );

        // Act
        var session = await handler.CreateItemAsync(
            new StartSessionCommand("user-id", "test.user", "TestApp"),
            "user-id",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("access-token", session.AccessToken?.Token);
        Assert.Equal(180, session.AccessToken?.ExpiresIn);
        Assert.Equal("refresh-token", session.RefreshToken);

        Assert.NotNull(stored);
        Assert.Equal("user-id", stored.UserId);
        Assert.Equal("refresh-token-hash", stored.Token);
        Assert.Equal(_now.AddSeconds(3600), stored.Expires);
        Assert.NotEqual(Guid.Empty, stored.Version);
        Assert.Equal("user-id", stored.CreatedBy);
        Assert.Equal(_now, stored.CreatedAt);
    }
}
