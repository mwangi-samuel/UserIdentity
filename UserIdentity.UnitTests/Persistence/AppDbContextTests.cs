using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.Roles;
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
    public void OnModelCreating_Should_Keep_The_Conventional_Keys()
    {
        // Act
        using var context = AppDbContextTestFactory.GetAppDbContext();

        // Assert
        var model = context.Model;

        Assert.Equal([nameof(UserEntity.Id)], PrimaryKey<UserEntity>(model));
        Assert.Equal(
            [nameof(IdentityUserLogin<>.LoginProvider), nameof(IdentityUserLogin<>.ProviderKey)],
            PrimaryKey<IdentityUserLogin<string>>(model)
        );
        Assert.Equal([nameof(IdentityUserRole<>.UserId), nameof(IdentityUserRole<>.RoleId)], PrimaryKey<IdentityUserRole<string>>(model));
        Assert.Equal(
            [nameof(IdentityUserToken<>.UserId), nameof(IdentityUserToken<>.LoginProvider), nameof(IdentityUserToken<>.Name)],
            PrimaryKey<IdentityUserToken<string>>(model)
        );
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

    private static int? MaxLength<TEntity>(IModel model, string propertyName) =>
        model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength();

    private static IEnumerable<string> PrimaryKey<TEntity>(IModel model) =>
        model.FindEntityType(typeof(TEntity))!.FindPrimaryKey()!.Properties.Select(property => property.Name);
}
