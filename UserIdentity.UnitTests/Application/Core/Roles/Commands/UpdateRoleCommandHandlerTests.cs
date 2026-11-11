using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Common.Exceptions;
using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.Roles;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Commands;

public class UpdateRoleCommandHandlerTests
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IInviteCodeRepository _inviteCodeRepository = A.Fake<IInviteCodeRepository>();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero));

    public UpdateRoleCommandHandlerTests()
    {
        _roleManager = A.Fake<RoleManager<IdentityRole>>();
    }

    [Fact]
    public async Task Update_NonExisting_Role_Throws_NoRecordException()
    {
        // Arrange
        UpdateRoleCommand command = new() { RoleId = "1", RoleName = "admin" };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(default(IdentityRole));

        UpdateRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository, _timeProvider);

        // Act& Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Update_Role_For_An_App_Admin_With_Another_Apps_Role_Throws_NoRecordException()
    {
        // Arrange
        UpdateRoleCommand command = new()
        {
            RoleId = "1",
            RoleName = "testapp:editor",
            AppName = "TestApp",
        };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(new IdentityRole { Id = "1", Name = "otherapp:editor" });

        UpdateRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _roleManager.UpdateAsync(A<IdentityRole>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Update_Role_For_An_App_Admin_To_A_Name_Outside_The_Apps_Prefix_Throws_InvalidDataException()
    {
        // Arrange
        UpdateRoleCommand command = new()
        {
            RoleId = "1",
            RoleName = "otherapp:admin",
            AppName = "TestApp",
        };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(new IdentityRole { Id = "1", Name = "testapp:editor" });

        UpdateRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _roleManager.UpdateAsync(A<IdentityRole>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Update_Existing_Role_Failure_Throws_RecordUpdateException()
    {
        // Arrange
        UpdateRoleCommand command = new() { RoleId = "1", RoleName = "admin" };

        IdentityRole identityRole = new() { Id = command.RoleId, Name = "User" };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(identityRole);

        A.CallTo(() => _roleManager.UpdateAsync(identityRole)).Returns(IdentityResult.Failed());

        UpdateRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository, _timeProvider);

        // Act& Assert
        await Assert.ThrowsAsync<RecordUpdateException>(() =>
            handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Update_Existing_Role_Updates_Successfully()
    {
        // Arrange
        UpdateRoleCommand command = new() { RoleId = "1", RoleName = "admin" };

        IdentityRole identityRole = new() { Id = command.RoleId, Name = "User" };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(identityRole);

        A.CallTo(() => _roleManager.UpdateAsync(identityRole)).Returns(IdentityResult.Success);

        UpdateRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository, _timeProvider);

        // Act
        var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<RoleViewModel>(vm);
        Assert.IsType<RoleDto>(vm.Role);
        Assert.Equal(command.RoleName, vm.Role.Name);
    }

    [Fact]
    public async Task Update_Role_Renames_The_Role_On_Its_Unused_Invites_In_The_Same_Save()
    {
        // Arrange
        UpdateRoleCommand command = new()
        {
            RoleId = "1",
            RoleName = "testapp:member",
            AppName = "TestApp",
        };

        IdentityRole identityRole = new() { Id = command.RoleId, Name = "testapp:user" };
        var inviteCode = new InviteCodeEntity
        {
            InviteCode = "ABCDEFGHJK",
            UserEmail = "invited@example.com",
            Role = "testapp:user",
        };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(identityRole);
        A.CallTo(() => _inviteCodeRepository.GetUnusedEntityItemsByRoleAsync("testapp:user", A<CancellationToken>._)).Returns([inviteCode]);
        A.CallTo(() => _roleManager.UpdateAsync(identityRole)).Returns(IdentityResult.Success);

        UpdateRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository, _timeProvider);

        // Act
        await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("testapp:member", inviteCode.Role);
        Assert.Equal(TestStringHelper.UserId, inviteCode.UpdatedBy);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, inviteCode.UpdatedAt);
        A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(inviteCode))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _roleManager.UpdateAsync(identityRole)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task Update_Role_Stores_The_Trimmed_Name_In_Lower_Case()
    {
        // Arrange
        UpdateRoleCommand command = new()
        {
            RoleId = "1",
            RoleName = " TestApp:Editor ",
            AppName = "TestApp",
        };

        IdentityRole identityRole = new() { Id = command.RoleId, Name = "testapp:user" };

        A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(identityRole);
        A.CallTo(() => _roleManager.UpdateAsync(identityRole)).Returns(IdentityResult.Success);

        UpdateRoleCommandHandler handler = new(_roleManager, _inviteCodeRepository, _timeProvider);

        // Act
        var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("testapp:editor", vm.Role.Name);
        Assert.Equal("testapp:editor", identityRole.Name);
    }
}
