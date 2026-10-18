using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Infrastructure.Utilities;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Settings;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class ExchangeRefreshTokenCommandHandlerTests
{
  private const string AccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1lIjoiSm9obiBEb2UiLCJpYXQiOjE1MTYyMzkwMjJ9.hqWGSaFpvbrXkOWc6lrnffhNWR19W_S1YKFBx2arWBk";
  private const string RefreshToken = "SamplevalidRefreshToken";
  private const string RefreshTokenHash = "SamplevalidRefreshTokenHash";

  private readonly IOptions<RefreshTokenSettings> _refreshTokenSettings;
  private readonly IJwtTokenHandler _jwtTokenHandler;
  private readonly ITokenFactory _tokenFactory;
  private readonly UserManager<IdentityUser> _userManager;
  private readonly IUnitOfWork _unitOfWork;
  private readonly IRefreshTokenRepository _refreshTokenRepository;
  private readonly IMachineDateTime _machineDateTime;

  private readonly IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> _getRoleClaimsQueryHandler;


  public ExchangeRefreshTokenCommandHandlerTests()
  {
    _refreshTokenSettings = Options.Create(new RefreshTokenSettings { ValidForSeconds = 2592000 });
    _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
    _tokenFactory = A.Fake<ITokenFactory>();
    _userManager = IdentityFakes.UserManager();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
    _machineDateTime = new MachineDateTime();
    _getRoleClaimsQueryHandler = A.Fake<IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels>>();

    A.CallTo(() => _tokenFactory.HashToken(RefreshToken)).Returns(RefreshTokenHash);
  }

  [Theory]
  [InlineData(null, "valid-user-name")]
  [InlineData("valid-user-id", null)]
  [InlineData(null, null)]
  public async Task ExchangeRefreshToken_With_Refresh_Token_With_No_Id_Or_Subject_Throws_SecurityTokenException(string? userId, string? userName)
  {
    // Arrange
    var tokenValidationResult = new TokenValidationResult
    {
    };

    var command = new ExchangeRefreshTokenCommand
    {
      AccessToken = "SampleInvalidAccessToken",
      RefreshToken = "SampleInvalidRefreshToken"
    };

    A.CallTo(() => _jwtTokenHandler.ValidateTokenAsync(command.AccessToken!, false)).Returns(Task.FromResult(tokenValidationResult));
    A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Sub)).Returns(userId);
    A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Name)).Returns(userName);
    A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Azp)).Returns((string?)null);

    var hanndler = GetExchangeRefreshTokenCommandHandler();

    // Act & Assert
    await Assert.ThrowsAsync<SecurityTokenException>(() => hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ExchangeRefreshToken_With_No_Existing_Refresh_Token_Throws_SecurityTokenException()
  {
    // Arrange
    var command = GetExchangeRefreshTokenCommand();

    SetUpValidAccessToken(command);

    A.CallTo(() => _refreshTokenRepository.GetEntityByAlternateIdAsync(
        A<RefreshTokenEntity>.That.Matches(e => e.UserId == TestStringHelper.UserId && e.Token == RefreshTokenHash),
        QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._)).Returns((RefreshTokenEntity?)null);
    A.CallTo(() => _refreshTokenRepository.GetEntityByPreviousTokenAsync(TestStringHelper.UserId, RefreshTokenHash, A<CancellationToken>._))
        .Returns((RefreshTokenEntity?)null);

    var hanndler = GetExchangeRefreshTokenCommandHandler();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<SecurityTokenException>(() => hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

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
      Expires = _machineDateTime.Now.AddDays(10),
    };

    SetUpValidAccessToken(command);

    A.CallTo(() => _refreshTokenRepository.GetEntityByAlternateIdAsync(A<RefreshTokenEntity>._, QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._))
        .Returns((RefreshTokenEntity?)null);
    A.CallTo(() => _refreshTokenRepository.GetEntityByPreviousTokenAsync(TestStringHelper.UserId, RefreshTokenHash, A<CancellationToken>._))
        .Returns(sessionEntity);

    var hanndler = GetExchangeRefreshTokenCommandHandler();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<SecurityTokenException>(() => hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

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
      Expires = _machineDateTime.Now.AddMinutes(-1),
    };

    SetUpValidAccessToken(command);

    A.CallTo(() => _refreshTokenRepository.GetEntityByAlternateIdAsync(
        A<RefreshTokenEntity>.That.Matches(e => e.UserId == TestStringHelper.UserId && e.Token == RefreshTokenHash),
        QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._)).Returns(dbRefreshToken);

    var hanndler = GetExchangeRefreshTokenCommandHandler();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<SecurityTokenException>(() => hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    Assert.Equal("Expired refresh token provided", exception.Message);
    A.CallTo(() => _refreshTokenRepository.RemoveEntityItem(dbRefreshToken)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _tokenFactory.GenerateToken(A<short>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task ExchangeRefreshToken_When_Update_Rfresh_Token_Returns_New_RefreshToken()
  {

    // Arrange
    var command = GetExchangeRefreshTokenCommand();

    var dbRefreshToken = new RefreshTokenEntity
    {
      Id = Guid.NewGuid(),
      UserId = TestStringHelper.UserId,
      Token = RefreshTokenHash,
      Expires = _machineDateTime.Now.AddMinutes(5),
    };

    var userRoles = new GetRoleClaimsForRolesQuery { Roles = ["Admin"] };
    var userRoleClaims = new RoleClaimsForRolesViewModels { RoleClaims = ["Admin"] };
    var updatedRefreshToken = RefreshToken + "updated";
    var updatedRefreshTokenHash = RefreshTokenHash + "updated";

    var newAccesToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1lIjoiSm9obiBEb2UiLCJpYXQiOjE1MTYyMzkwMjJ9.tL95SMCv9-I_ApoP8DKhhCHd2YcbscFNWo5feRsOwnQ";
    var newAccesstokenExpiresIn = 90000;

    SetUpValidAccessToken(command);

    A.CallTo(() => _refreshTokenRepository.GetEntityByAlternateIdAsync(
        A<RefreshTokenEntity>.That.Matches(e => e.UserId == TestStringHelper.UserId && e.Token == RefreshTokenHash),
        QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._)).Returns(dbRefreshToken);

    A.CallTo(() => _userManager.GetRolesAsync(A<IdentityUser>.That.Matches(x => x.Id == TestStringHelper.UserId))).Returns(userRoles.Roles);

    A.CallTo(() => _getRoleClaimsQueryHandler.GetItemsAsync(userRoles, A<CancellationToken>._)).Returns(userRoleClaims);
    A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns(updatedRefreshToken);
    A.CallTo(() => _tokenFactory.HashToken(updatedRefreshToken)).Returns(updatedRefreshTokenHash);


    A.CallTo(() => _jwtTokenHandler.CreateToken(TestStringHelper.UserId, TestStringHelper.UserName, "TestApp", userRoles.Roles.ToHashSet(), userRoleClaims.RoleClaims, A<HashSet<Claim>?>._)).Returns((newAccesToken, newAccesstokenExpiresIn));

    var hanndler = GetExchangeRefreshTokenCommandHandler();

    // Act
    var vm = await hanndler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<ExchangeRefreshTokenViewModel>(vm);
    Assert.IsType<AccessTokenViewModel>(vm.UserToken);
    Assert.IsType<AccessTokenDTO>(vm.UserToken.AccessToken);

    Assert.Equal(newAccesToken, vm.UserToken.AccessToken?.Token);
    Assert.Equal(newAccesstokenExpiresIn, vm.UserToken.AccessToken?.ExpiresIn);
    Assert.Equal(updatedRefreshToken, vm.UserToken.RefreshToken);

    A.CallTo(() => _refreshTokenRepository.UpdateEntityItem(A<RefreshTokenEntity>.That.Matches(e =>
        e.Token == updatedRefreshTokenHash && e.PreviousToken == RefreshTokenHash && e.Expires > DateTime.UtcNow.AddDays(29) &&
        e.UserId == TestStringHelper.UserId && e.UpdatedBy == TestStringHelper.UserId && e.UpdatedAt != null)))
        .MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }


  private static ExchangeRefreshTokenCommand GetExchangeRefreshTokenCommand()
  {
    return new ExchangeRefreshTokenCommand
    {
      AccessToken = AccessToken,
      RefreshToken = RefreshToken
    };
  }

  private void SetUpValidAccessToken(ExchangeRefreshTokenCommand command)
  {
    var tokenValidationResult = new TokenValidationResult
    {
    };

    A.CallTo(() => _jwtTokenHandler.ValidateTokenAsync(command.AccessToken!, false)).Returns(Task.FromResult(tokenValidationResult));
    A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Sub)).Returns(TestStringHelper.UserId);
    A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Name)).Returns(TestStringHelper.UserName);
    A.CallTo(() => _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Azp)).Returns("TestApp");
  }

  private ExchangeRefreshTokenCommandHandler GetExchangeRefreshTokenCommandHandler()
  {
    return new ExchangeRefreshTokenCommandHandler(
        _refreshTokenSettings,
        _jwtTokenHandler,
        _tokenFactory,
        _userManager,
        _unitOfWork,
        _refreshTokenRepository,
        _machineDateTime,
        _getRoleClaimsQueryHandler
    );
  }

}
