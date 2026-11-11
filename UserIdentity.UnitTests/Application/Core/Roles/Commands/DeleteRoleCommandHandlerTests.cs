using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Common.Exceptions;
using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Commands;

public class DeleteRoleCommandHandlerTests
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IInviteCodeRepository _inviteCodeRepository = A.Fake<IInviteCodeRepository>();

    public DeleteRoleCommandHandlerTests()
    {
        _roleManager = A.Fake<RoleManager<IdentityRole>>();
    }

    [Fact]
    public async Task DeleteRole_When_Non_Existent_Role_Throws_NoRecordException()
    {
        // Arrange
        DeleteRoleCommand command = new() { RoleId = "SampleInvalidId" };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(default(IdentityRole));

        DeleteRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            handler.DeleteItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task DeleteRole_For_An_App_Admin_With_Another_Apps_Role_Throws_NoRecordException()
    {
        // Arrange
        DeleteRoleCommand command = new() { RoleId = "Role123", AppName = "TestApp" };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId))
            .Returns(new IdentityRole { Id = command.RoleId, Name = "otherapp:admin" });

        DeleteRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            handler.DeleteItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _roleManager.DeleteAsync(A<IdentityRole>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DeleteRole_Granted_By_An_Unused_Invite_Throws_IllegalOperationException()
    {
        // Arrange
        DeleteRoleCommand command = new() { RoleId = "Role123", AppName = "TestApp" };

        IdentityRole role = new() { Id = command.RoleId, Name = "testapp:owner" };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(role);
        A.CallTo(() => _inviteCodeRepository.GetUnusedEntityItemsByRoleAsync("testapp:owner", A<CancellationToken>._))
            .Returns([
                new InviteCodeEntity
                {
                    InviteCode = "ABCDEFGHJK",
                    UserEmail = "invited@example.com",
                    Role = "testapp:owner",
                },
            ]);

        DeleteRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            handler.DeleteItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal("A role that unused invite codes grant can't be deleted", exception.Message);
        A.CallTo(() => _roleManager.DeleteAsync(A<IdentityRole>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DeleteRole_Failure_When_Deleting_Role_Throws_RecordDeletionException()
    {
        // Arrange
        DeleteRoleCommand command = new() { RoleId = "SampleValidId" };

        IdentityRole role = new() { Id = command.RoleId, Name = "SampleRole" };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(role);
        A.CallTo(() => _roleManager.DeleteAsync(role)).Returns(IdentityResult.Failed());

        DeleteRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository);

        // Act & Assert
        await Assert.ThrowsAsync<RecordDeletionException>(() =>
            handler.DeleteItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task DeleteRole_When_Existing_Role_Deletes_Role()
    {
        // Arrange
        DeleteRoleCommand command = new() { RoleId = "SampleValidId" };

        IdentityRole role = new() { Id = command.RoleId, Name = "SampleRole" };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(role);
        A.CallTo(() => _roleManager.DeleteAsync(role)).Returns(IdentityResult.Success);

        DeleteRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository);

        // Act
        var vm = await handler.DeleteItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<DeleteRecordViewModel>(vm);
    }
}
