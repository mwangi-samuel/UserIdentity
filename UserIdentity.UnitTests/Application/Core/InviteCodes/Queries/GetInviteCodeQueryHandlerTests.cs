using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.InviteCodes.Queries;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.InviteCodes.Queries;

public class GetInviteCodeQueryHandlerTests
{
  private readonly IInviteCodeRepository _inviteCodeRepository;

  public GetInviteCodeQueryHandlerTests()
  {
    _inviteCodeRepository = A.Fake<IInviteCodeRepository>();
  }

  [Fact]
  public async Task GetItemAsync_With_InviteCodeId_Returns_InviteCode()
  {
    // Arrange
    var inviteCodeId = 1L;
    var expectedDto = new InviteCodeDTO
    {
      Id = inviteCodeId,
      InviteCode = "INVITE123",
      UserEmail = "test@example.com",
      AppId = 1,
      AppName = "TestApp"
    };

    A.CallTo(() => _inviteCodeRepository.GetEntityDTOByIdAsync(inviteCodeId, A<CancellationToken>._)).Returns(expectedDto);

    var query = new GetInviteCodeQuery { InviteCodeId = inviteCodeId, AppId = 1 };
    var handler = new GetInviteCodeQueryHandler(_inviteCodeRepository);

    // Act
    var result = await handler.GetItemAsync(query, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.InviteCode);
    Assert.Equal(expectedDto.Id, result.InviteCode.Id);
    Assert.Equal(expectedDto.InviteCode, result.InviteCode.InviteCode);
    Assert.Equal(expectedDto.UserEmail, result.InviteCode.UserEmail);
    Assert.Equal(expectedDto.AppName, result.InviteCode.AppName);

    A.CallTo(() => _inviteCodeRepository.GetEntityDTOByIdAsync(inviteCodeId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(A<InviteCodeEntity>._, A<QueryCondition>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task GetItemAsync_With_UserEmail_Returns_InviteCode()
  {
    // Arrange
    var userEmail = "test@example.com";
    var entity = new InviteCodeEntity
    {
      Id = 1,
      InviteCode = "INVITE456",
      UserEmail = userEmail,
      AppId = 2,
      App = new PolyzenKit.Domain.RegisteredApps.RegisteredAppEntity { AppName = "TestApp" }
    };

    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(
        A<InviteCodeEntity>.That.Matches(e => e.UserEmail == userEmail && e.AppId == 2),
        QueryCondition.MUST_EXIST, A<CancellationToken>._)).Returns(entity);

    var query = new GetInviteCodeQuery { UserEmail = userEmail, AppId = 2 };
    var handler = new GetInviteCodeQueryHandler(_inviteCodeRepository);

    // Act
    var result = await handler.GetItemAsync(query, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.InviteCode);
    Assert.Equal(entity.Id, result.InviteCode.Id);
    Assert.Equal(entity.InviteCode, result.InviteCode.InviteCode);
    Assert.Equal(entity.UserEmail, result.InviteCode.UserEmail);

    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(
        A<InviteCodeEntity>.That.Matches(e => e.UserEmail == userEmail && e.AppId == 2),
        QueryCondition.MUST_EXIST, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _inviteCodeRepository.GetEntityDTOByIdAsync(A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task GetItemAsync_With_InviteCodeId_Of_Another_App_Throws_NoRecordException()
  {
    // Arrange
    var inviteCodeId = 1L;

    A.CallTo(() => _inviteCodeRepository.GetEntityDTOByIdAsync(inviteCodeId, A<CancellationToken>._))
        .Returns(new InviteCodeDTO { Id = inviteCodeId, InviteCode = "INVITE123", UserEmail = "test@example.com", AppId = 5 });

    var query = new GetInviteCodeQuery { InviteCodeId = inviteCodeId, AppId = 1 };
    var handler = new GetInviteCodeQueryHandler(_inviteCodeRepository);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemAsync(query, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetItemAsync_With_InviteCodeId_When_Not_Found_Throws_NoRecordException()
  {
    // Arrange
    var inviteCodeId = 999L;

    A.CallTo(() => _inviteCodeRepository.GetEntityDTOByIdAsync(inviteCodeId, A<CancellationToken>._))
        .ThrowsAsync(new NoRecordException($"{inviteCodeId}", "InviteCode"));

    var query = new GetInviteCodeQuery { InviteCodeId = inviteCodeId, AppId = 1 };
    var handler = new GetInviteCodeQueryHandler(_inviteCodeRepository);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemAsync(query, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetItemAsync_With_UserEmail_When_Not_Found_Throws_NoRecordException()
  {
    // Arrange
    var userEmail = "notfound@example.com";

    A.CallTo(() => _inviteCodeRepository.GetEntityByAlternateIdAsync(
        A<InviteCodeEntity>.That.Matches(e => e.UserEmail == userEmail && e.AppId == 2),
        QueryCondition.MUST_EXIST, A<CancellationToken>._))
        .ThrowsAsync(new NoRecordException(userEmail, nameof(InviteCodeEntity)));

    var query = new GetInviteCodeQuery { UserEmail = userEmail, AppId = 2 };
    var handler = new GetInviteCodeQueryHandler(_inviteCodeRepository);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemAsync(query, TestContext.Current.CancellationToken));
  }
}
