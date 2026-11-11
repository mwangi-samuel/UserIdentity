using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class ExchangeRefreshTokenCommandHandlerTests
{
    private const string AccessToken =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1lIjoiSm9obiBEb2UiLCJpYXQiOjE1MTYyMzkwMjJ9.hqWGSaFpvbrXkOWc6lrnffhNWR19W_S1YKFBx2arWBk";
    private const string RefreshToken = "SamplevalidRefreshToken";
    private const string RefreshTokenHash = "SamplevalidRefreshTokenHash";
    private const int RefreshTokenValidForSeconds = 2592000;

    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly IOptions<RefreshTokenSettings> _refreshTokenSettings;
    private readonly IJwtTokenHandler _jwtTokenHandler;
    private readonly ITokenFactory _tokenFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly FakeTimeProvider _timeProvider;

    private readonly ICreateItemCommandHandler<CreateAccessTokenCommand, AccessTokenViewModel> _createAccessTokenCommandHandler = A.Fake<
        ICreateItemCommandHandler<CreateAccessTokenCommand, AccessTokenViewModel>
    >();

    public ExchangeRefreshTokenCommandHandlerTests()
    {
        _refreshTokenSettings = Options.Create(new RefreshTokenSettings { ValidForSeconds = RefreshTokenValidForSeconds });
        _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
        _tokenFactory = A.Fake<ITokenFactory>();
        _unitOfWork = A.Fake<IUnitOfWork>();
        _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(_now);

        A.CallTo(() => _tokenFactory.HashToken(RefreshToken)).Returns(RefreshTokenHash);
    }

    [Theory]
    [InlineData(null, "valid-user-name")]
    [InlineData("valid-user-id", null)]
    [InlineData(null, null)]
    public async Task ExchangeRefreshToken_With_Refresh_Token_With_No_Id_Or_Subject_Throws_SecurityTokenException(
        string? userId,
        string? userName
    )
    {
        // Arrange
        var tokenValidationResult = new TokenValidationResult { };

        var command = new ExchangeRefreshTokenCommand
        {
            AccessToken = "SampleInvalidAccessToken",
            RefreshToken = "SampleInvalidRefreshToken",
        };

        A.CallTo(() => _jwtTokenHandler.ValidateTokenAsync(command.AccessToken, false)).Returns(Task.FromResult(tokenValidationResult));
        A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Sub)).Returns(userId);
        A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Name)).Returns(userName);
        A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Azp))
            .Returns((string?)null);

        var hanndler = GetExchangeRefreshTokenCommandHandler();

        // Act & Assert
        await Assert.ThrowsAsync<SecurityTokenException>(() =>
            hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ExchangeRefreshToken_With_No_Existing_Refresh_Token_Throws_SecurityTokenException()
    {
        // Arrange
        var command = GetExchangeRefreshTokenCommand();

        SetUpValidAccessToken(command);
        SetUpRefreshTokenLookup(null);

        A.CallTo(() =>
                _refreshTokenRepository.GetEntityByPreviousTokenAsync(TestStringHelper.UserId, RefreshTokenHash, A<CancellationToken>._)
            )
            .Returns((RefreshTokenEntity?)null);

        var hanndler = GetExchangeRefreshTokenCommandHandler();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<SecurityTokenException>(() =>
            hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Invalid refresh token provided", exception.Message);
        A.CallTo(() => _refreshTokenRepository.RemoveEntityItem(A<RefreshTokenEntity>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ExchangeRefreshToken_With_Replaced_Refresh_Token_Removes_Session_And_Throws_SecurityTokenException()
    {
        // Arrange
        var command = GetExchangeRefreshTokenCommand();

        var sessionEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = TestStringHelper.UserId,
            Token = "CurrentRefreshTokenHash",
            PreviousToken = RefreshTokenHash,
            Expires = _now.AddDays(10),
        };

        SetUpValidAccessToken(command);
        SetUpRefreshTokenLookup(null);

        A.CallTo(() =>
                _refreshTokenRepository.GetEntityByPreviousTokenAsync(TestStringHelper.UserId, RefreshTokenHash, A<CancellationToken>._)
            )
            .Returns(sessionEntity);

        var hanndler = GetExchangeRefreshTokenCommandHandler();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<SecurityTokenException>(() =>
            hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Invalid refresh token provided", exception.Message);
        A.CallTo(() => _refreshTokenRepository.RemoveEntityItem(sessionEntity)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _refreshTokenRepository.UpdateEntityItem(A<RefreshTokenEntity>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ExchangeRefreshToken_With_Expired_Refresh_Token_Removes_It_And_Throws_SecurityTokenException()
    {
        // Arrange
        var command = GetExchangeRefreshTokenCommand();

        var dbRefreshToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = TestStringHelper.UserId,
            Token = RefreshTokenHash,
            Expires = _now.AddMinutes(-1),
        };

        SetUpValidAccessToken(command);
        SetUpRefreshTokenLookup((dbRefreshToken, true));

        var hanndler = GetExchangeRefreshTokenCommandHandler();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<SecurityTokenException>(() =>
            hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Expired refresh token provided", exception.Message);
        A.CallTo(() => _refreshTokenRepository.RemoveEntityItem(dbRefreshToken)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _tokenFactory.GenerateToken(A<int>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ExchangeRefreshToken_When_Update_Rfresh_Token_Returns_New_RefreshToken()
    {
        // Arrange
        var command = GetExchangeRefreshTokenCommand();

        var originalVersion = Guid.NewGuid();
        var dbRefreshToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = TestStringHelper.UserId,
            Token = RefreshTokenHash,
            Expires = _now.AddMinutes(5),
            Version = originalVersion,
        };

        var updatedRefreshToken = RefreshToken + "updated";
        var updatedRefreshTokenHash = RefreshTokenHash + "updated";

        var newAccesToken =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1lIjoiSm9obiBEb2UiLCJpYXQiOjE1MTYyMzkwMjJ9.tL95SMCv9-I_ApoP8DKhhCHd2YcbscFNWo5feRsOwnQ";
        var newAccesstokenExpiresIn = 90000;

        SetUpValidAccessToken(command);
        SetUpRefreshTokenLookup((dbRefreshToken, true));
        SetUpNewAccessToken(newAccesToken, newAccesstokenExpiresIn);

        A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns(updatedRefreshToken);
        A.CallTo(() => _tokenFactory.HashToken(updatedRefreshToken)).Returns(updatedRefreshTokenHash);

        var hanndler = GetExchangeRefreshTokenCommandHandler();

        // Act
        var vm = await hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(newAccesToken, vm.UserToken.AccessToken.Token);
        Assert.Equal(newAccesstokenExpiresIn, vm.UserToken.AccessToken.ExpiresIn);
        Assert.Equal(updatedRefreshToken, vm.UserToken.RefreshToken);

        Assert.Equal(updatedRefreshTokenHash, dbRefreshToken.Token);
        Assert.Equal(RefreshTokenHash, dbRefreshToken.PreviousToken);
        Assert.Equal(_now.AddSeconds(RefreshTokenValidForSeconds), dbRefreshToken.Expires);
        Assert.Equal(TestStringHelper.UserId, dbRefreshToken.UpdatedBy);
        Assert.Equal(_now, dbRefreshToken.UpdatedAt);
        Assert.NotEqual(originalVersion, dbRefreshToken.Version);
        Assert.NotEqual(Guid.Empty, dbRefreshToken.Version);

        A.CallTo(() => _refreshTokenRepository.UpdateEntityItem(dbRefreshToken)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExchangeRefreshToken_When_The_Save_Hits_A_Concurrency_Conflict_Throws_DbUpdateConcurrencyException()
    {
        // Arrange
        var command = GetExchangeRefreshTokenCommand();

        var dbRefreshToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = TestStringHelper.UserId,
            Token = RefreshTokenHash,
            Expires = _now.AddMinutes(5),
            Version = Guid.NewGuid(),
        };

        SetUpValidAccessToken(command);
        SetUpRefreshTokenLookup((dbRefreshToken, true));
        SetUpNewAccessToken("new-access-token", 180);

        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).ThrowsAsync(new DbUpdateConcurrencyException());

        var hanndler = GetExchangeRefreshTokenCommandHandler();

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() =>
                _refreshTokenRepository.GetEntityForAppAsync(TestStringHelper.UserId, RefreshTokenHash, "TestApp", A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExchangeRefreshToken_For_An_App_The_User_Isnt_Registered_For_Removes_The_Session_And_Throws_SecurityTokenException()
    {
        // Arrange
        var command = GetExchangeRefreshTokenCommand();

        var dbRefreshToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = TestStringHelper.UserId,
            Token = RefreshTokenHash,
            Expires = _now.AddMinutes(5),
        };

        SetUpValidAccessToken(command);
        SetUpRefreshTokenLookup((dbRefreshToken, false));

        var hanndler = GetExchangeRefreshTokenCommandHandler();

        // Act & Assert
        await Assert.ThrowsAsync<SecurityTokenException>(() =>
            hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _refreshTokenRepository.RemoveEntityItem(dbRefreshToken)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _tokenFactory.GenerateToken(A<int>._)).MustNotHaveHappened();
    }

    private static ExchangeRefreshTokenCommand GetExchangeRefreshTokenCommand() =>
        new() { AccessToken = AccessToken, RefreshToken = RefreshToken };

    private void SetUpValidAccessToken(ExchangeRefreshTokenCommand command)
    {
        var tokenValidationResult = new TokenValidationResult { };

        A.CallTo(() => _jwtTokenHandler.ValidateTokenAsync(command.AccessToken, false)).Returns(Task.FromResult(tokenValidationResult));
        A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Sub))
            .Returns(TestStringHelper.UserId);
        A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Name))
            .Returns(TestStringHelper.UserName);
        A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Azp)).Returns("TestApp");
    }

    private void SetUpRefreshTokenLookup((RefreshTokenEntity Entity, bool IsUserRegisteredForApp)? lookup) =>
        A.CallTo(() =>
                _refreshTokenRepository.GetEntityForAppAsync(TestStringHelper.UserId, RefreshTokenHash, "TestApp", A<CancellationToken>._)
            )
            .Returns(lookup);

    private void SetUpNewAccessToken(string token, int expiresIn) =>
        A.CallTo(() =>
                _createAccessTokenCommandHandler.CreateItemAsync(
                    new CreateAccessTokenCommand(TestStringHelper.UserId, TestStringHelper.UserName, "TestApp"),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .Returns(
                new AccessTokenViewModel
                {
                    AccessToken = new AccessTokenDto { Token = token, ExpiresIn = expiresIn },
                }
            );

    private ExchangeRefreshTokenCommandHandler GetExchangeRefreshTokenCommandHandler()
    {
        return new ExchangeRefreshTokenCommandHandler(
            _refreshTokenSettings,
            _jwtTokenHandler,
            _tokenFactory,
            _unitOfWork,
            _refreshTokenRepository,
            _timeProvider,
            _createAccessTokenCommandHandler
        );
    }
}
