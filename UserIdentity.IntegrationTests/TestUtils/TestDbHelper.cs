using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.Users;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Persistence;

namespace UserIdentity.IntegrationTests.TestUtils;

public sealed class TestDbHelper(
  IConfiguration configuration,
  AppDbContext appDbContext,
  UserManager<IdentityUser> userManager,
  RoleManager<IdentityRole> roleManager
  )
{

  private readonly string _currentAppNameNormalized = "testapp";
  private readonly RoleSettings _roleSettings = configuration.GetSetting<RoleSettings>();
  private readonly AppDbContext _appDbContext = appDbContext;
  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;
  private readonly TokenFactory _tokenFactory = new();

  public string DefaultRole => $"{_currentAppNameNormalized}{ZenConstants.SERVICE_ROLE_SEPARATOR}{_roleSettings.DefaultRole}";
  public HashSet<string> AdminRoles => [.. _roleSettings.AdminRoles.Split(',').Select(r => $"{_currentAppNameNormalized}{ZenConstants.SERVICE_ROLE_SEPARATOR}{r.Trim()}")];

  #region IdentityUser Related
  public IdentityUser CreateIdentityUser()
  {

    DeleteIdentityUser(UserSettingHelper.UserId);

    var user = new IdentityUser
    {
      UserName = UserSettingHelper.UserName,
      PhoneNumber = UserSettingHelper.PhoneNumber,
      Id = UserSettingHelper.UserId,
      Email = UserSettingHelper.UserEmail,
      EmailConfirmed = true,
      NormalizedEmail = UserSettingHelper.UserEmail.ToUpper(),
      NormalizedUserName = UserSettingHelper.UserName.ToUpper(),
      ConcurrencyStamp = DateTime.Now.Ticks.ToString(),
    };

    user.PasswordHash = new PasswordHasher<IdentityUser>().HashPassword(user, UserSettingHelper.UserPassword);

    _appDbContext.Users.Add(user);
    _appDbContext.SaveChanges();

    return user;
  }

  public void UpdateIdentityUser(IdentityUser user)
  {
    _appDbContext.Users.Update(user);
    _appDbContext.SaveChanges();
  }

  public IdentityUser? GetIdentityUser(string userId)
  {
    return _appDbContext.Users.FirstOrDefault(u => u.Id == userId);
  }

  public IdentityUser? GetIdentityUserWithNoTracking(string userId)
  {
    return _appDbContext.Users.AsNoTracking().FirstOrDefault(u => u.Id == userId);
  }

  public void DeleteIdentityUser(string userId)
  {
    var user = GetIdentityUser(userId);

    if (user != null)
    {
      _appDbContext.Users.Remove(user);
      _appDbContext.SaveChanges();
    }
  }

  #endregion


  #region IndentityRole Related
  public IdentityRole CreateIdentityRole(string roleName)
  {
    DeleteIdnetityRole(roleName);
    var role = new IdentityRole
    {
      Name = roleName,
      NormalizedName = roleName.ToUpper()
    };

    _appDbContext.Roles.Add(role);
    _appDbContext.SaveChanges();

    return role;
  }

  public void UpdateIdentityRole(IdentityRole role)
  {
    _appDbContext.Roles.Update(role);
    _appDbContext.SaveChanges();
  }

  public IdentityRole? GetIdentityRole(string roleName)
  {
    return _appDbContext.Roles.FirstOrDefault(r => r.Name != null && r.Name.Equals(roleName));
  }

  public void DeleteIdnetityRole(string roleName)
  {
    var role = GetIdentityRole(roleName);

    if (role != null)
    {
      _appDbContext.Remove(role);
      _appDbContext.SaveChanges();
    }
  }

  #endregion

  #region IdentityUserRole Related
  public IdentityUserRole<string> CreateIdentityUserRole(string roleName, string userId)
  {
    var existingRole = GetIdentityRole(roleName) ?? throw new ArgumentNullException(nameof(roleName));
    var existingUser = GetIdentityUser(userId) ?? throw new ArgumentNullException(nameof(userId));

    var userRole = new IdentityUserRole<string>
    {
      RoleId = existingRole.Id,
      UserId = existingUser.Id
    };

    _appDbContext.UserRoles.Add(userRole);
    _appDbContext.SaveChanges();

    return userRole;
  }

  public IdentityUserRole<string> ConfigureIdentityUserAsAdmin()
  {
    return CreateIdentityUserRole(AdminRoles.First(), UserSettingHelper.UserId);
  }

  public IdentityUserRole<string>? GetIdentityUserRole(string roleName, string userId)
  {
    var role = GetIdentityRole(roleName);
    if (role != null)
    {
      return _appDbContext.UserRoles.FirstOrDefault(ur => ur.UserId == userId && ur.RoleId == role.Id);
    }
    return null;
  }

  public void DeleteIdentityUserRole(string roleName, string userId)
  {
    var userRole = GetIdentityUserRole(roleName, userId);

    if (userRole != null)
    {
      _appDbContext.Remove(userRole);
      _appDbContext.SaveChanges();
    }
  }

  #endregion

  #region UserEntity Related 
  public UserEntity CreateUserEntity()
  {
    DeleteUserEntity(UserSettingHelper.UserId);

    var user = new UserEntity
    {
      Id = UserSettingHelper.UserId.ToString(),
      FirstName = UserSettingHelper.FirstName,
      LastName = UserSettingHelper.LastName,
      CreatedBy = UserSettingHelper.UserId.ToString(),
      CreatedAt = DateTime.UtcNow,
      UpdatedBy = UserSettingHelper.UserId.ToString(),
      UpdatedAt = DateTime.UtcNow
    };

    _appDbContext.AppUser.Add(user);
    _appDbContext.SaveChanges();

    return user;
  }

  public UserEntity? GetUserEntity(string userId)
  {
    return _appDbContext.AppUser.FirstOrDefault(x => x.Id == userId);
  }

  public void DeleteUserEntity(string userId)
  {
    var user = GetUserEntity(userId);

    if (user != null)
    {
      _appDbContext.AppUser.Remove(user);
      _appDbContext.SaveChanges();
    }

  }

  #endregion

  #region RefreshTokenEntity Related 
  public RefreshTokenEntity CreateRefreshTokenEntity()
  {
    DeleteRefreshTokenEntity(UserSettingHelper.UserId);

    var refreshToken = new RefreshTokenEntity
    {
      Id = Guid.CreateVersion7(),
      UserId = UserSettingHelper.UserId.ToString(),
      Token = StringUtil.GenerateRandomString(32),
      CreatedBy = UserSettingHelper.UserId.ToString(),
      CreatedAt = DateTime.UtcNow,
      UpdatedBy = UserSettingHelper.UserId.ToString(),
      UpdatedAt = DateTime.UtcNow
    };

    _appDbContext.RefreshToken.Add(refreshToken);
    _appDbContext.SaveChanges();

    return refreshToken;
  }


  public RefreshTokenEntity? GetRefreshTokenEntity(string userId)
  {
    return _appDbContext.RefreshToken.FirstOrDefault(r => r.UserId == userId);
  }

  public List<RefreshTokenEntity> GetRefreshTokenEntitiesWithNoTracking(string userId)
  {
    return [.. _appDbContext.RefreshToken.AsNoTracking().Where(r => r.UserId == userId)];
  }

  public void ExpireRefreshTokenEntities(string userId)
  {
    foreach (var refreshToken in _appDbContext.RefreshToken.Where(r => r.UserId == userId))
      refreshToken.Expires = DateTime.UtcNow.AddMinutes(-1);

    _appDbContext.SaveChanges();
  }

  public void DeleteRefreshTokenEntity(string userId)
  {
    var refreshToken = GetRefreshTokenEntity(userId);

    if (refreshToken != null)
    {
      _appDbContext.RefreshToken.Remove(refreshToken);
      _appDbContext.SaveChanges();
    }
  }

  #endregion

  #region PasswordResetEntity Related
  public PasswordResetEntity CreatePasswordResetEntity(string resetRequestId, bool verified = true, bool applied = false, DateTime? expires = null)
  {
    var entity = new PasswordResetEntity
    {
      Id = Guid.CreateVersion7(),
      RequestId = _tokenFactory.HashToken(resetRequestId),
      UserId = UserSettingHelper.UserId,
      Expires = expires ?? DateTime.UtcNow.AddMinutes(15),
      Verified = verified,
      Applied = applied,
      CreatedBy = UserSettingHelper.UserId,
      CreatedAt = DateTime.UtcNow,
      UpdatedBy = UserSettingHelper.UserId,
      UpdatedAt = DateTime.UtcNow
    };

    _appDbContext.PasswordReset.Add(entity);
    _appDbContext.SaveChanges();

    return entity;
  }

  public PasswordResetEntity? GetPasswordResetEntityWithNoTracking(string userId)
  {
    return _appDbContext.PasswordReset.AsNoTracking()
      .Where(x => x.UserId == userId)
      .OrderByDescending(x => x.CreatedAt)
      .FirstOrDefault();
  }

  #endregion

  #region RegisteredAppEntity Related 

  public RegisteredAppEntity GetRegisteredAppEntityByAppName(string appName)
  {
    return _appDbContext.RegisteredApp
      .Where(a => a.AppName.Equals(appName))
      .SingleOrDefault() ?? throw new InvalidOperationException("No related registered app exists");

  }

  #endregion


  #region InviteCodeEntity Related
  public InviteCodeEntity CreateInviteCodeEntity(string userEmail, string appName, string? inviteCode = null, bool applied = false)
  {
    DeleteInviteCodeEntityByEmail(userEmail);

    var registeredApp = GetRegisteredAppEntityByAppName(appName);

    var entity = new InviteCodeEntity
    {
      InviteCode = inviteCode ?? StringUtil.GenerateRandomString(8).ToUpper(),
      UserEmail = userEmail,
      AppId = registeredApp.Id,
      Applied = applied,
      CreatedBy = UserSettingHelper.UserId,
      CreatedAt = DateTime.UtcNow,
      UpdatedBy = UserSettingHelper.UserId,
      UpdatedAt = DateTime.UtcNow,
      IsDeleted = false
    };

    _appDbContext.InviteCode.Add(entity);
    _appDbContext.SaveChanges();

    return entity;
  }

  public InviteCodeEntity? GetInviteCodeEntity(long id)
  {
    return _appDbContext.InviteCode.FirstOrDefault(x => x.Id == id);
  }

  public InviteCodeEntity? GetInviteCodeEntityByEmail(string userEmail)
  {
    return _appDbContext.InviteCode.FirstOrDefault(x => x.UserEmail == userEmail);
  }

  public void DeleteInviteCodeEntity(long id)
  {
    var entity = GetInviteCodeEntity(id);

    if (entity != null)
    {
      _appDbContext.InviteCode.Remove(entity);
      _appDbContext.SaveChanges();
    }
  }

  public void DeleteInviteCodeEntityByEmail(string userEmail)
  {
    var entity = GetInviteCodeEntityByEmail(userEmail);

    if (entity != null)
    {
      _appDbContext.InviteCode.Remove(entity);
      _appDbContext.SaveChanges();
    }
  }

  #endregion

  #region WaitListEntity Related
  public WaitListEntity CreateWaitListEntity(string userEmail, string appName)
  {
    DeleteWaitListEntityByEmail(userEmail);

    var registeredApp = GetRegisteredAppEntityByAppName(appName);

    var entity = new WaitListEntity
    {
      UserEmail = userEmail,
      AppId = registeredApp.Id,
      CreatedBy = UserSettingHelper.UserId,
      CreatedAt = DateTime.UtcNow,
      UpdatedBy = UserSettingHelper.UserId,
      UpdatedAt = DateTime.UtcNow,
      IsDeleted = false
    };

    _appDbContext.WaitList.Add(entity);
    _appDbContext.SaveChanges();

    return entity;
  }

  public WaitListEntity? GetWaitListEntity(long id)
  {
    return _appDbContext.WaitList.FirstOrDefault(x => x.Id == id);
  }

  public WaitListEntity? GetWaitListEntityByEmail(string userEmail)
  {
    return _appDbContext.WaitList.FirstOrDefault(x => x.UserEmail == userEmail);
  }

  public void DeleteWaitListEntity(long id)
  {
    var entity = GetWaitListEntity(id);

    if (entity != null)
    {
      _appDbContext.WaitList.Remove(entity);
      _appDbContext.SaveChanges();
    }
  }

  public void DeleteWaitListEntityByEmail(string userEmail)
  {
    var entity = GetWaitListEntityByEmail(userEmail);

    if (entity != null)
    {
      _appDbContext.WaitList.Remove(entity);
      _appDbContext.SaveChanges();
    }
  }

  #endregion

  #region  Helper Methods 
  public bool? UpdateRoleClaim(string roleName, string scopeClaim)
  {
    var role = GetIdentityRole(roleName) ?? throw new ArgumentNullException(nameof(roleName));

    var roleClaim = new Claim(JwtCustomClaimNames.Scope, scopeClaim);

    var result = _roleManager.AddClaimAsync(role, roleClaim);

    return result.Result.Succeeded;
  }

  public void SeedDatabase()
  {
    var identityUser = CreateIdentityUser();
    CreateIdentityRole(DefaultRole);
    CreateIdentityUserRole(DefaultRole, identityUser.Id);
    CreateUserEntity();
    CreateRefreshTokenEntity();
  }
  #endregion

}
