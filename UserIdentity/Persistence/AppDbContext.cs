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
  public DbSet<UserEntity> AppUser { get; internal set; }

  public DbSet<RefreshTokenEntity> RefreshToken { get; internal set; }

  public DbSet<AppEntity> AppEntity { get; internal set; }

  public DbSet<RegisteredAppEntity> RegisteredApp { get; internal set; }

  public DbSet<UserRegisteredAppEntity> UserRegisteredApp { get; internal set; }

  public DbSet<WaitListEntity> WaitList { get; internal set; }

  public DbSet<InviteCodeEntity> InviteCode { get; internal set; }

  public DbSet<PasswordResetEntity> PasswordReset { get; internal set; }

  public DbSet<OutboxMessageEntity> OutboxMessage { get; internal set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.ApplyConfiguration(new IdentityUserConfiguration());
    modelBuilder.ApplyConfiguration(new UserConfiguration());
    modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
    modelBuilder.ApplyConfiguration(new AppEntityConfiguration());
    modelBuilder.ApplyConfiguration(new RegisteredAppConfiguration());
    modelBuilder.ApplyConfiguration(new UserRegisteredAppConfiguration());
    modelBuilder.ApplyConfiguration(new WaitListConfiguration());
    modelBuilder.ApplyConfiguration(new InviteCodeConfiguration());
    modelBuilder.ApplyConfiguration(new PasswordResetConfiguration());
    modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

    modelBuilder.Entity<UserEntity>().ToTable("user_details");
    modelBuilder.Entity<RefreshTokenEntity>().ToTable("refresh_tokens");
    modelBuilder.Entity<AppEntity>().ToTable("app_entities");
    modelBuilder.Entity<RegisteredAppEntity>().ToTable("registered_apps");
    modelBuilder.Entity<UserRegisteredAppEntity>().ToTable("user_registered_apps");
    modelBuilder.Entity<WaitListEntity>().ToTable("wait_lists");
    modelBuilder.Entity<InviteCodeEntity>().ToTable("invite_codes");
    modelBuilder.Entity<PasswordResetEntity>().ToTable("password_resets");
    modelBuilder.Entity<OutboxMessageEntity>().ToTable("outbox_messages");

    modelBuilder.Entity<IdentityUser>().ToTable("users");
    modelBuilder.Entity<IdentityRole>().ToTable("roles");
    modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
    modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("role_claims");
    modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("user_logins").HasKey(i => new { i.LoginProvider, i.ProviderKey });
    modelBuilder.Entity<IdentityUserRole<string>>().ToTable("user_roles").HasKey(i => new { i.UserId, i.RoleId });
    modelBuilder.Entity<IdentityUserToken<string>>().ToTable("user_tokens").HasKey(i => new { i.UserId, i.LoginProvider, i.Name });
  }
}
