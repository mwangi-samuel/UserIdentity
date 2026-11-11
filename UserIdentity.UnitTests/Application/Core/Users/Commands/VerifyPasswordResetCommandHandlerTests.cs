using FakeItEasy;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Commands;
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
    private readonly FakeTimeProvider _timeProvider;

    public VerifyPasswordResetCommandHandlerTests()
    {
        _tokenFactory = A.Fake<ITokenFactory>();
        _unitOfWork = A.Fake<IUnitOfWork>();
        _passwordResetRepository = A.Fake<IPasswordResetRepository>();
        _timeProvider = new FakeTimeProvider();

        A.CallTo(() => _tokenFactory.HashToken(ResetRequestId)).Returns(ResetRequestIdHash);
    }

    [Fact]
    public async Task UpdateItemAsync_WhenRequestNotFound_ThrowsNoRecordException()
    {
        // Arrange
        A.CallTo(() =>
                _passwordResetRepository.GetEntityByAlternateIdAsync(
                    A<PasswordResetEntity>._,
                    QueryCondition.MustExist,
                    A<CancellationToken>._
                )
            )
            .Throws(NoRecordException.ForIdentifier(ResetRequestIdHash));

        var handler = new VerifyPasswordResetCommandHandler(_tokenFactory, _unitOfWork, _passwordResetRepository, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            handler.UpdateItemAsync(
                new VerifyPasswordResetCommand { ResetRequestId = ResetRequestId },
                TestStringHelper.UserId,
                TestContext.Current.CancellationToken
            )
        );

        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateItemAsync_WhenRequestExists_MarksRequestVerified()
    {
        // Arrange
        var existingEntity = new PasswordResetEntity
        {
            Id = Guid.NewGuid(),
            RequestId = ResetRequestIdHash,
            UserId = "user-id",
            Verified = false,
        };

        A.CallTo(() =>
                _passwordResetRepository.GetEntityByAlternateIdAsync(
                    A<PasswordResetEntity>.That.Matches(e => e.RequestId == ResetRequestIdHash),
                    QueryCondition.MustExist,
                    A<CancellationToken>._
                )
            )
            .Returns(existingEntity);

        var handler = new VerifyPasswordResetCommandHandler(_tokenFactory, _unitOfWork, _passwordResetRepository, _timeProvider);

        // Act
        await handler.UpdateItemAsync(
            new VerifyPasswordResetCommand { ResetRequestId = ResetRequestId },
            TestStringHelper.UserId,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(existingEntity.Verified);
        Assert.Equal(existingEntity.UserId, existingEntity.UpdatedBy);
        A.CallTo(() => _passwordResetRepository.UpdateEntityItem(existingEntity)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
