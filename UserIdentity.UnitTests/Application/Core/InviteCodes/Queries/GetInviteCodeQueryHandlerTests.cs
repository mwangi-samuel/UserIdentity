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
        var expectedDto = new InviteCodeDto
        {
            Id = inviteCodeId,
            InviteCode = "INVITE123",
            UserEmail = "test@example.com",
            AppId = 1,
            AppName = "TestApp",
        };

        A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(inviteCodeId, A<CancellationToken>._)).Returns(expectedDto);

        var query = new GetInviteCodeQuery { InviteCodeId = inviteCodeId, AppId = 1 };
        var handler = new GetInviteCodeQueryHandler(_inviteCodeRepository);

        // Act
        var result = await handler.GetItemAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedDto.Id, result.InviteCode.Id);
        Assert.Equal(expectedDto.InviteCode, result.InviteCode.InviteCode);
        Assert.Equal(expectedDto.UserEmail, result.InviteCode.UserEmail);
        Assert.Equal(expectedDto.AppName, result.InviteCode.AppName);

        A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(inviteCodeId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _inviteCodeRepository.GetEntityDtoByEmailAsync(A<int>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetItemAsync_With_UserEmail_Returns_InviteCode()
    {
        // Arrange
        var userEmail = "test@example.com";
        var expectedDto = new InviteCodeDto
        {
            Id = 1,
            InviteCode = "INVITE456",
            UserEmail = userEmail,
            AppId = 2,
            AppName = "TestApp",
        };

        A.CallTo(() => _inviteCodeRepository.GetEntityDtoByEmailAsync(2, userEmail, A<CancellationToken>._)).Returns(expectedDto);

        var query = new GetInviteCodeQuery { UserEmail = userEmail, AppId = 2 };
        var handler = new GetInviteCodeQueryHandler(_inviteCodeRepository);

        // Act
        var result = await handler.GetItemAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expectedDto, result.InviteCode);
        A.CallTo(() =>
                _inviteCodeRepository.GetEntityByAlternateIdAsync(A<InviteCodeEntity>._, A<QueryCondition>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
        A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetItemAsync_With_InviteCodeId_Of_Another_App_Throws_NoRecordException()
    {
        // Arrange
        var inviteCodeId = 1L;

        A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(inviteCodeId, A<CancellationToken>._))
            .Returns(
                new InviteCodeDto
                {
                    Id = inviteCodeId,
                    InviteCode = "INVITE123",
                    UserEmail = "test@example.com",
                    AppId = 5,
                }
            );

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

        A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(inviteCodeId, A<CancellationToken>._))
            .ThrowsAsync(NoRecordException.ForIdentifier($"{inviteCodeId}"));

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

        A.CallTo(() => _inviteCodeRepository.GetEntityDtoByEmailAsync(2, userEmail, A<CancellationToken>._)).Returns((InviteCodeDto?)null);

        var query = new GetInviteCodeQuery { UserEmail = userEmail, AppId = 2 };
        var handler = new GetInviteCodeQueryHandler(_inviteCodeRepository);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NoRecordException>(() =>
            handler.GetItemAsync(query, TestContext.Current.CancellationToken)
        );

        Assert.Equal($"No record exists for the provided identifier - {userEmail}", exception.Message);
    }
}
