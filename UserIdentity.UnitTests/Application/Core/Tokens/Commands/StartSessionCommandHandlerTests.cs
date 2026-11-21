using FakeItEasy;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
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

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 7,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private RefreshTokenEntity? _stored;
    private CreateAccessTokenCommand? _accessTokenCommand;

    public StartSessionCommandHandlerTests()
    {
        _timeProvider.SetUtcNow(_now);

        A.CallTo(() => _createAccessTokenCommandHandler.CreateItemAsync(A<CreateAccessTokenCommand>._, A<string>._, A<CancellationToken>._))
            .Invokes((CreateAccessTokenCommand command, string _, CancellationToken _) => _accessTokenCommand = command)
            .Returns(
                new AccessTokenViewModel
                {
                    AccessToken = new AccessTokenDto { Token = "access-token", ExpiresIn = 180 },
                }
            );
        A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns("refresh-token");
        A.CallTo(() => _tokenFactory.HashToken("refresh-token")).Returns("refresh-token-hash");
        A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>._))
            .Invokes((RefreshTokenEntity entity) => _stored = entity);
    }

    [Fact]
    public async Task Start_Session_Adds_A_Hashed_Refresh_Token_For_The_App_That_Expires_After_The_Configured_Time()
    {
        // Act
        var session = await StartSessionAsync();

        // Assert
        Assert.Equal("access-token", session.UserToken.AccessToken.Token);
        Assert.Equal(180, session.UserToken.AccessToken.ExpiresIn);
        Assert.Equal("refresh-token", session.UserToken.RefreshToken);

        Assert.NotNull(_stored);
        Assert.Equal("user-id", _stored.UserId);
        Assert.Equal(_registeredApp.Id, _stored.AppId);
        Assert.Equal("refresh-token-hash", _stored.Token);
        Assert.Equal(_now.AddSeconds(3600), _stored.Expires);
        Assert.NotEqual(Guid.Empty, _stored.Version);
        Assert.Equal("user-id", _stored.CreatedBy);
        Assert.Equal(_now, _stored.CreatedAt);
    }

    [Fact]
    public async Task Start_Session_Stores_The_Requests_Address_And_Device()
    {
        // Act
        await StartSessionAsync();

        // Assert
        Assert.NotNull(_stored);
        Assert.Equal("203.0.113.7", _stored.IpAddress);
        Assert.Equal("Test Browser", _stored.Device);
    }

    [Fact]
    public async Task Start_Session_Gives_The_Access_Token_The_Refresh_Tokens_Id_As_Its_Session_Id()
    {
        // Act
        await StartSessionAsync();

        // Assert
        Assert.NotNull(_stored);
        Assert.NotEqual(Guid.Empty, _stored.Id);
        Assert.Equal(new CreateAccessTokenCommand("user-id", "test.user", "TestApp", _stored.Id), _accessTokenCommand);
    }

    private Task<UserTokenViewModel> StartSessionAsync() =>
        new StartSessionCommandHandler(
            Options.Create(new RefreshTokenSettings { ValidForSeconds = 3600 }),
            _createAccessTokenCommandHandler,
            _refreshTokenRepository,
            _tokenFactory,
            _timeProvider
        ).CreateItemAsync(
            new StartSessionCommand("user-id", "test.user", _registeredApp, RequestContext.Create("203.0.113.7", "Test Browser")),
            "user-id",
            TestContext.Current.CancellationToken
        );
}
