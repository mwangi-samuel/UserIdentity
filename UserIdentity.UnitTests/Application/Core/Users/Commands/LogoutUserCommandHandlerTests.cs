using System;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class LogoutUserCommandHandlerTests
{
  private const string RefreshToken = "SampleRefreshToken";
  private const string RefreshTokenHash = "SampleRefreshTokenHash";

  private readonly ITokenFactory _tokenFactory;
  private readonly IUnitOfWork _unitOfWork;
  private readonly IRefreshTokenRepository _refreshTokenRepository;

  public LogoutUserCommandHandlerTests()
  {
    _tokenFactory = A.Fake<ITokenFactory>();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();

    A.CallTo(() => _tokenFactory.HashToken(RefreshToken)).Returns(RefreshTokenHash);
  }

  [Fact]
  public async Task Logout_With_Existing_Refresh_Token_Removes_Refresh_Token()
  {
    // Arrange
    var command = new LogoutUserCommand { RefreshToken = RefreshToken };
    var existingEntity = new RefreshTokenEntity { Id = Guid.NewGuid(), UserId = TestStringHelper.UserId, Token = RefreshTokenHash };

    A.CallTo(() => _refreshTokenRepository.GetEntityByAlternateIdAsync(
        A<RefreshTokenEntity>.That.Matches(e => e.UserId == TestStringHelper.UserId && e.Token == RefreshTokenHash),
        QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._)).Returns(existingEntity);

    var handler = new LogoutUserCommandHandler(_tokenFactory, _unitOfWork, _refreshTokenRepository);

    // Act
    await handler.DeleteItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => _refreshTokenRepository.RemoveEntityItem(existingEntity)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Logout_With_Unknown_Refresh_Token_Does_Nothing()
  {
    // Arrange
    var command = new LogoutUserCommand { RefreshToken = RefreshToken };

    A.CallTo(() => _refreshTokenRepository.GetEntityByAlternateIdAsync(A<RefreshTokenEntity>._, QueryCondition.MAY_OR_MAY_NOT_EXIST, A<CancellationToken>._))
        .Returns((RefreshTokenEntity?)null);

    var handler = new LogoutUserCommandHandler(_tokenFactory, _unitOfWork, _refreshTokenRepository);

    // Act
    await handler.DeleteItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => _refreshTokenRepository.RemoveEntityItem(A<RefreshTokenEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }
}
