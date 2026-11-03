using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using PolyzenKit.Domain.Entities.AppEntities;
using PolyzenKit.Domain.Entities.OutboxMessages;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Configurations;

using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Domain.Users;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Persistence.Configurations;

namespace UserIdentity.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> dbContextOptions
    ) : IdentityDbContext<IdentityUser>(dbContextOptions)
{
  public DbSet<UserEntity> AppUser => Set<UserEntity>();

  public DbSet<RefreshTokenEntity> RefreshToken => Set<RefreshTokenEntity>();

  public DbSet<AppEntity> AppEntity => Set<AppEntity>();

  public DbSet<RegisteredAppEntity> RegisteredApp => Set<RegisteredAppEntity>();

  public DbSet<UserRegisteredAppEntity> UserRegisteredApp => Set<UserRegisteredAppEntity>();

  public DbSet<WaitListEntity> WaitList => Set<WaitListEntity>();

  public DbSet<InviteCodeEntity> InviteCode => Set<InviteCodeEntity>();

  public DbSet<PasswordResetEntity> PasswordReset => Set<PasswordResetEntity>();

  public DbSet<OutboxMessageEntity> OutboxMessage => Set<OutboxMessageEntity>();

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    builder.ApplyConfiguration(new IdentityUserConfiguration());
    builder.ApplyConfiguration(new UserConfiguration());
    builder.ApplyConfiguration(new RefreshTokenConfiguration());
    builder.ApplyConfiguration(new AppEntityConfiguration());
    builder.ApplyConfiguration(new RegisteredAppConfiguration());
    builder.ApplyConfiguration(new UserRegisteredAppConfiguration());
    builder.ApplyConfiguration(new WaitListConfiguration());
    builder.ApplyConfiguration(new InviteCodeConfiguration());
    builder.ApplyConfiguration(new PasswordResetConfiguration());
    builder.ApplyConfiguration(new OutboxMessageConfiguration());

    builder.Entity<UserEntity>().ToTable("user_details");
    builder.Entity<RefreshTokenEntity>().ToTable("refresh_tokens");
    builder.Entity<AppEntity>().ToTable("app_entities");
    builder.Entity<RegisteredAppEntity>().ToTable("registered_apps");
    builder.Entity<UserRegisteredAppEntity>().ToTable("user_registered_apps");
    builder.Entity<WaitListEntity>().ToTable("wait_lists");
    builder.Entity<InviteCodeEntity>().ToTable("invite_codes");
    builder.Entity<PasswordResetEntity>().ToTable("password_resets");
    builder.Entity<OutboxMessageEntity>().ToTable("outbox_messages");

    builder.Entity<IdentityUser>().ToTable("users");
    builder.Entity<IdentityRole>().ToTable("roles");
    builder.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
    builder.Entity<IdentityRoleClaim<string>>().ToTable("role_claims");
    builder.Entity<IdentityUserLogin<string>>().ToTable("user_logins").HasKey(i => new { i.LoginProvider, i.ProviderKey });
    builder.Entity<IdentityUserRole<string>>().ToTable("user_roles").HasKey(i => new { i.UserId, i.RoleId });
    builder.Entity<IdentityUserToken<string>>().ToTable("user_tokens").HasKey(i => new { i.UserId, i.LoginProvider, i.Name });
  }
}
