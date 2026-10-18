using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using PolyzenKit.Domain.AppEntities;
using PolyzenKit.Domain.RegisteredApps;
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
  private readonly string _entityKeyPrefix = "";

  public DbSet<UserEntity> AppUser { get; internal set; }

  public DbSet<RefreshTokenEntity> RefreshToken { get; internal set; }

  public DbSet<AppEntity> AppEntity { get; internal set; }

  public DbSet<RegisteredAppEntity> RegisteredApp { get; internal set; }

  public DbSet<UserRegisteredAppEntity> UserRegisteredApp { get; internal set; }

  public DbSet<WaitListEntity> WaitList { get; internal set; }

  public DbSet<InviteCodeEntity> InviteCode { get; internal set; }

  public DbSet<PasswordResetEntity> PasswordReset { get; internal set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.ApplyConfiguration(new UserConfiguration());
    modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
    modelBuilder.ApplyConfiguration(new AppEntityConfiguration());
    modelBuilder.ApplyConfiguration(new RegisteredAppConfiguration());
    modelBuilder.ApplyConfiguration(new UserRegisteredAppConfiguration());
    modelBuilder.ApplyConfiguration(new WaitListConfiguration());
    modelBuilder.ApplyConfiguration(new InviteCodeConfiguration());
    modelBuilder.ApplyConfiguration(new PasswordResetConfiguration());

    modelBuilder.Entity<UserEntity>().ToTable(_entityKeyPrefix + "user_details");
    modelBuilder.Entity<RefreshTokenEntity>().ToTable(_entityKeyPrefix + "refresh_tokens");
    modelBuilder.Entity<AppEntity>().ToTable(_entityKeyPrefix + "app_entities");
    modelBuilder.Entity<RegisteredAppEntity>().ToTable(_entityKeyPrefix + "registered_apps");
    modelBuilder.Entity<UserRegisteredAppEntity>().ToTable(_entityKeyPrefix + "user_registered_apps");
    modelBuilder.Entity<WaitListEntity>().ToTable(_entityKeyPrefix + "wait_lists");
    modelBuilder.Entity<InviteCodeEntity>().ToTable(_entityKeyPrefix + "invite_codes");
    modelBuilder.Entity<PasswordResetEntity>().ToTable(_entityKeyPrefix + "password_resets");

    modelBuilder.Entity<IdentityUser>().ToTable(_entityKeyPrefix + "users");
    modelBuilder.Entity<IdentityRole>().ToTable(_entityKeyPrefix + "roles");
    modelBuilder.Entity<IdentityUserClaim<string>>().ToTable(_entityKeyPrefix + "user_claims");
    modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable(_entityKeyPrefix + "role_claims");
    modelBuilder.Entity<IdentityUserLogin<string>>().ToTable(_entityKeyPrefix + "user_logins").HasKey(i => new { i.LoginProvider, i.ProviderKey });
    modelBuilder.Entity<IdentityUserRole<string>>().ToTable(_entityKeyPrefix + "user_roles").HasKey(i => new { i.UserId, i.RoleId });
    modelBuilder.Entity<IdentityUserToken<string>>().ToTable(_entityKeyPrefix + "user_tokens").HasKey(i => new { i.UserId, i.LoginProvider, i.Name });
  }
}
