using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PolyzenKit.Domain.Entities.AppEntities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.Roles;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Domain.Users;
using UserIdentity.Domain.WaitLists;
using Xunit;

namespace UserIdentity.UnitTests.Persistence;

public class AppDbContextTests
{
    [Fact]
    public void Constructor_Turns_Off_Automatic_Savepoints()
    {
        // Act
        using var context = AppDbContextTestFactory.GetAppDbContext();

        // Assert
        Assert.False(context.Database.AutoSavepointsEnabled);
    }

    [Fact]
    public void OnModelCreating_Should_Take_Column_Lengths_From_The_Limits_Classes()
    {
        // Act
        using var context = AppDbContextTestFactory.GetAppDbContext();

        // Assert
        var model = context.Model;

        Assert.Equal(UserLimits.FirstNameMaxLength, MaxLength<UserEntity>(model, nameof(UserEntity.FirstName)));
        Assert.Equal(UserLimits.LastNameMaxLength, MaxLength<UserEntity>(model, nameof(UserEntity.LastName)));
        Assert.Equal(UserLimits.UserNameMaxLength, MaxLength<IdentityUser>(model, nameof(IdentityUser.UserName)));
        Assert.Equal(UserLimits.UserNameMaxLength, MaxLength<IdentityUser>(model, nameof(IdentityUser.NormalizedUserName)));
        Assert.Equal(UserLimits.EmailMaxLength, MaxLength<IdentityUser>(model, nameof(IdentityUser.Email)));
        Assert.Equal(UserLimits.EmailMaxLength, MaxLength<IdentityUser>(model, nameof(IdentityUser.NormalizedEmail)));
        Assert.Equal(UserLimits.PhoneNumberMaxLength, MaxLength<IdentityUser>(model, nameof(IdentityUser.PhoneNumber)));
        Assert.Equal(RoleLimits.NameMaxLength, MaxLength<IdentityRole>(model, nameof(IdentityRole.Name)));
        Assert.Equal(RoleLimits.NameMaxLength, MaxLength<IdentityRole>(model, nameof(IdentityRole.NormalizedName)));
        Assert.Equal(InviteCodeLimits.InviteCodeMaxLength, MaxLength<InviteCodeEntity>(model, nameof(InviteCodeEntity.InviteCode)));
        Assert.Equal(InviteCodeLimits.UserEmailMaxLength, MaxLength<InviteCodeEntity>(model, nameof(InviteCodeEntity.UserEmail)));
        Assert.Equal(InviteCodeLimits.RoleMaxLength, MaxLength<InviteCodeEntity>(model, nameof(InviteCodeEntity.Role)));
        Assert.Equal(WaitListLimits.UserEmailMaxLength, MaxLength<WaitListEntity>(model, nameof(WaitListEntity.UserEmail)));
        Assert.Equal(RefreshTokenLimits.TokenMaxLength, MaxLength<RefreshTokenEntity>(model, nameof(RefreshTokenEntity.Token)));
        Assert.Equal(
            RefreshTokenLimits.PreviousTokenMaxLength,
            MaxLength<RefreshTokenEntity>(model, nameof(RefreshTokenEntity.PreviousToken))
        );
        Assert.Equal(PasswordResetLimits.RequestIdMaxLength, MaxLength<PasswordResetEntity>(model, nameof(PasswordResetEntity.RequestId)));
    }

    [Fact]
    public void OnModelCreating_Should_Make_The_Refresh_Token_Version_A_Concurrency_Token()
    {
        // Act
        using var context = AppDbContextTestFactory.GetAppDbContext();

        // Assert
        var version = context.Model.FindEntityType(typeof(RefreshTokenEntity))!.FindProperty(nameof(RefreshTokenEntity.Version))!;

        Assert.True(version.IsConcurrencyToken);
    }

    [Fact]
    public void OnModelCreating_Should_Update_Model()
    {
        // Arrange
        var entityPrefix = "";
        var stringType = "<string>";

        // Act
        using var context = AppDbContextTestFactory.GetAppDbContext();

        var entityTypes = context.Model.GetEntityTypes();

        // Assert
        Assert.Equal(
            entityPrefix + "app_entities",
            entityTypes.Where(e => e.DisplayName() == nameof(AppEntity)).FirstOrDefault()?.GetTableName()
        );
        Assert.Equal(
            entityPrefix + "user_details",
            entityTypes.Where(e => e.DisplayName() == nameof(UserEntity)).FirstOrDefault()?.GetTableName()
        );
        Assert.Equal(
            entityPrefix + "refresh_tokens",
            entityTypes.Where(e => e.DisplayName() == nameof(RefreshTokenEntity)).FirstOrDefault()?.GetTableName()
        );
        Assert.Equal(
            entityPrefix + "registered_apps",
            entityTypes.Where(e => e.DisplayName() == nameof(RegisteredAppEntity)).FirstOrDefault()?.GetTableName()
        );
        Assert.Equal(
            entityPrefix + "user_registered_apps",
            entityTypes.Where(e => e.DisplayName() == nameof(UserRegisteredAppEntity)).FirstOrDefault()?.GetTableName()
        );
        Assert.Equal(
            entityPrefix + "wait_lists",
            entityTypes.Where(e => e.DisplayName() == nameof(WaitListEntity)).FirstOrDefault()?.GetTableName()
        );
        Assert.Equal(
            entityPrefix + "invite_codes",
            entityTypes.Where(e => e.DisplayName() == nameof(InviteCodeEntity)).FirstOrDefault()?.GetTableName()
        );
        Assert.Equal(
            entityPrefix + "password_resets",
            entityTypes.Where(e => e.DisplayName() == nameof(PasswordResetEntity)).FirstOrDefault()?.GetTableName()
        );

        Assert.Equal(
            entityPrefix + "users",
            entityTypes.Where(e => e.DisplayName() == nameof(IdentityUser)).FirstOrDefault()?.GetTableName()
        );
        Assert.Equal(
            entityPrefix + "roles",
            entityTypes.Where(e => e.DisplayName() == nameof(IdentityRole)).FirstOrDefault()?.GetTableName()
        );

        Assert.Equal(
            entityPrefix + "user_claims",
            entityTypes.Where(e => e.DisplayName() == nameof(IdentityUserClaim<>) + stringType).FirstOrDefault()?.GetTableName()
        );
        Assert.Equal(
            entityPrefix + "role_claims",
            entityTypes.Where(e => e.DisplayName() == nameof(IdentityRoleClaim<>) + stringType).FirstOrDefault()?.GetTableName()
        );

        Assert.Equal(
            entityPrefix + "user_logins",
            entityTypes.Where(e => e.DisplayName() == nameof(IdentityUserLogin<>) + stringType).FirstOrDefault()?.GetTableName()
        );
        Assert.Equal(
            entityPrefix + "user_roles",
            entityTypes.Where(e => e.DisplayName() == nameof(IdentityUserRole<>) + stringType).FirstOrDefault()?.GetTableName()
        );

        Assert.Equal(
            entityPrefix + "user_tokens",
            entityTypes.Where(e => e.DisplayName() == nameof(IdentityUserToken<>) + stringType).FirstOrDefault()?.GetTableName()
        );
    }

    private static int? MaxLength<TEntity>(IModel model, string propertyName) =>
        model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength();
}
