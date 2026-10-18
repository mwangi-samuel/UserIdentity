using System;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class VerifyPasswordResetCommandHandlerTests
{
  private const string ResetRequestId = "SampleResetRequestId";
  private const string ResetRequestIdHash = "SampleResetRequestIdHash";

  private readonly ITokenFactory _tokenFactory;
  private readonly IUnitOfWork _unitOfWork;
  private readonly IPasswordResetRepository _passwordResetRepository;
  private readonly IMachineDateTime _machineDateTime;

  public VerifyPasswordResetCommandHandlerTests()
  {
    _tokenFactory = A.Fake<ITokenFactory>();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _passwordResetRepository = A.Fake<IPasswordResetRepository>();
    _machineDateTime = A.Fake<IMachineDateTime>();

    A.CallTo(() => _tokenFactory.HashToken(ResetRequestId)).Returns(ResetRequestIdHash);
  }

  [Fact]
  public async Task UpdateItemAsync_WhenRequestNotFound_ThrowsNoRecordException()
  {
    A.CallTo(() => _passwordResetRepository.GetEntityByAlternateIdAsync(A<PasswordResetEntity>._, QueryCondition.MUST_EXIST, A<CancellationToken>._))
      .Throws(new NoRecordException(ResetRequestIdHash, nameof(PasswordResetEntity)));

    var handler = new VerifyPasswordResetCommandHandler(_tokenFactory, _unitOfWork, _passwordResetRepository, _machineDateTime);

    await Assert.ThrowsAsync<NoRecordException>(() => handler.UpdateItemAsync(new VerifyPasswordResetCommand { ResetRequestId = ResetRequestId }, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task UpdateItemAsync_WhenRequestExists_MarksRequestVerified()
  {
    var existingEntity = new PasswordResetEntity { Id = Guid.NewGuid(), RequestId = ResetRequestIdHash, UserId = "user-id", Verified = false };

    A.CallTo(() => _passwordResetRepository.GetEntityByAlternateIdAsync(
      A<PasswordResetEntity>.That.Matches(e => e.RequestId == ResetRequestIdHash), QueryCondition.MUST_EXIST, A<CancellationToken>._))
      .Returns(existingEntity);

    var handler = new VerifyPasswordResetCommandHandler(_tokenFactory, _unitOfWork, _passwordResetRepository, _machineDateTime);

    var vm = await handler.UpdateItemAsync(new VerifyPasswordResetCommand { ResetRequestId = ResetRequestId }, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    Assert.IsType<VerifyPasswordResetViewModel>(vm);
    Assert.True(vm.VerifyPasswordResetResult.PasswordResetVerified);
    Assert.True(existingEntity.Verified);
    Assert.Equal(existingEntity.UserId, existingEntity.UpdatedBy);
    A.CallTo(() => _passwordResetRepository.UpdateEntityItem(existingEntity)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }
}
