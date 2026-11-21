using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Queries;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
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

    private readonly IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> _getRegisteredAppHandler = A.Fake<
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel>
    >();

    private readonly IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel> _getTwoFactorStatusQueryHandler = A.Fake<
        IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel>
    >();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

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
        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "TestApp"),
                    A<CancellationToken>._
                )
            )
            .Returns(new RegisteredAppViewModel { RegisteredApp = _registeredApp });
        A.CallTo(() =>
                _getTwoFactorStatusQueryHandler.GetItemAsync(
                    new GetTwoFactorStatusQuery(TestStringHelper.UserId, _registeredApp),
                    A<CancellationToken>._
                )
            )
            .Returns(new TwoFactorStatusViewModel { AllowedMethods = [] });
    }

    [Fact]
    public async Task ExchangeRefreshToken_When_Two_Step_Sign_In_Is_Required_And_Not_Set_Up_Ends_The_Session_And_Throws_SecurityTokenException()
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
        SetUpRefreshTokenLookup((dbRefreshToken, true));
        A.CallTo(() => _getTwoFactorStatusQueryHandler.GetItemAsync(A<GetTwoFactorStatusQuery>._, A<CancellationToken>._))
            .Returns(new TwoFactorStatusViewModel { Required = true, AllowedMethods = [TwoFactorMethod.Authenticator] });

        // Act & Assert
        await Assert.ThrowsAsync<SecurityTokenException>(() =>
            GetExchangeRefreshTokenCommandHandler().UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _refreshTokenRepository.RemoveEntityItem(dbRefreshToken)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _createAccessTokenCommandHandler.CreateItemAsync(A<CreateAccessTokenCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ExchangeRefreshToken_When_Two_Step_Sign_In_Is_Required_And_Set_Up_Issues_A_New_Token()
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
        SetUpRefreshTokenLookup((dbRefreshToken, true));
        SetUpNewAccessToken("new-access-token", 180, dbRefreshToken.Id);
        A.CallTo(() => _getTwoFactorStatusQueryHandler.GetItemAsync(A<GetTwoFactorStatusQuery>._, A<CancellationToken>._))
            .Returns(
                new TwoFactorStatusViewModel
                {
                    Required = true,
                    Method = TwoFactorMethod.Authenticator,
                    AllowedMethods = [TwoFactorMethod.Authenticator],
                }
            );

        // Act
        var vm = await GetExchangeRefreshTokenCommandHandler()
            .UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("new-access-token", vm.UserToken.AccessToken.Token);
        A.CallTo(() => _refreshTokenRepository.RemoveEntityItem(A<RefreshTokenEntity>._)).MustNotHaveHappened();
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
        SetUpNewAccessToken(newAccesToken, newAccesstokenExpiresIn, dbRefreshToken.Id);

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
        SetUpNewAccessToken("new-access-token", 180, dbRefreshToken.Id);

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
    public async Task ExchangeRefreshToken_For_A_Session_That_Isnt_Of_The_Tokens_App_Removes_The_Session_And_Throws_SecurityTokenException()
    {
        // Arrange
        var command = GetExchangeRefreshTokenCommand();

        var dbRefreshToken = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = TestStringHelper.UserId,
            AppId = 2,
            Token = RefreshTokenHash,
            Expires = _now.AddMinutes(5),
        };

        SetUpValidAccessToken(command);
        SetUpRefreshTokenLookup((dbRefreshToken, false));

        var hanndler = GetExchangeRefreshTokenCommandHandler();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<SecurityTokenException>(() =>
            hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Invalid refresh token provided", exception.Message);
        A.CallTo(() => _refreshTokenRepository.RemoveEntityItem(dbRefreshToken)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _createAccessTokenCommandHandler.CreateItemAsync(A<CreateAccessTokenCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _tokenFactory.GenerateToken(A<int>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ExchangeRefreshToken_Records_The_Requests_Address_And_Device_And_Keeps_The_Session_Id()
    {
        // Arrange
        var command = new ExchangeRefreshTokenCommand
        {
            AccessToken = AccessToken,
            RefreshToken = RefreshToken,
            RequestContext = RequestContext.Create("203.0.113.7", "Test Browser"),
        };

        var sessionId = Guid.CreateVersion7();
        var dbRefreshToken = new RefreshTokenEntity
        {
            Id = sessionId,
            UserId = TestStringHelper.UserId,
            AppId = _registeredApp.Id,
            Token = RefreshTokenHash,
            Expires = _now.AddMinutes(5),
            IpAddress = "198.51.100.20",
            Device = "Old Browser",
        };

        SetUpValidAccessToken(command);
        SetUpRefreshTokenLookup((dbRefreshToken, true));
        SetUpNewAccessToken("new-access-token", 180, sessionId);

        // Act
        var vm = await GetExchangeRefreshTokenCommandHandler()
            .UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("new-access-token", vm.UserToken.AccessToken.Token);
        Assert.Equal(sessionId, dbRefreshToken.Id);
        Assert.Equal("203.0.113.7", dbRefreshToken.IpAddress);
        Assert.Equal("Test Browser", dbRefreshToken.Device);
        A.CallTo(() =>
                _createAccessTokenCommandHandler.CreateItemAsync(
                    new CreateAccessTokenCommand(TestStringHelper.UserId, TestStringHelper.UserName, "TestApp", sessionId),
                    TestStringHelper.UserId,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _refreshTokenRepository.UpdateEntityItem(dbRefreshToken)).MustHaveHappenedOnceExactly();
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

    private void SetUpRefreshTokenLookup((RefreshTokenEntity Entity, bool IsForApp)? lookup) =>
        A.CallTo(() =>
                _refreshTokenRepository.GetEntityForAppAsync(TestStringHelper.UserId, RefreshTokenHash, "TestApp", A<CancellationToken>._)
            )
            .Returns(lookup);

    private void SetUpNewAccessToken(string token, int expiresIn, Guid sessionId) =>
        A.CallTo(() =>
                _createAccessTokenCommandHandler.CreateItemAsync(
                    new CreateAccessTokenCommand(TestStringHelper.UserId, TestStringHelper.UserName, "TestApp", sessionId),
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
            _getRegisteredAppHandler,
            _getTwoFactorStatusQueryHandler,
            _createAccessTokenCommandHandler
        );
    }
}
