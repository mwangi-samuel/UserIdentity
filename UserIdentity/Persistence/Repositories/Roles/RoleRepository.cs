using Microsoft.EntityFrameworkCore;

using UserIdentity.Domain.Roles;

namespace UserIdentity.Persistence.Repositories.Roles;

public class RoleRepository(AppDbContext appDbContext) : IRoleRepository
{
  private readonly AppDbContext _appDbContext = appDbContext;

  public async Task<List<RoleDto>> GetRoleDtosAsync(string? namePrefix, CancellationToken cancellationToken = default)
  {
    var normalizedPrefix = namePrefix?.ToUpperInvariant();

    return await _appDbContext.Roles
      .Where(role => normalizedPrefix == null || role.NormalizedName!.StartsWith(normalizedPrefix))
      .OrderBy(role => role.Name).ThenBy(role => role.Id)
      .Select(role => new RoleDto { Id = role.Id, Name = role.Name! })
      .ToListAsync(cancellationToken);
  }

  public async Task<List<string>> GetUserRoleNamesAsync(string userId, CancellationToken cancellationToken = default)
  {
    return await _appDbContext.UserRoles
      .Where(userRole => userRole.UserId == userId)
      .Join(_appDbContext.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
      .OrderBy(roleName => roleName)
      .ToListAsync(cancellationToken);
  }

  public async Task<HashSet<string>> GetScopesAsync(
    IReadOnlyCollection<string> roleNames,
    CancellationToken cancellationToken = default)
  {
    if (roleNames.Count == 0)
      return [];

    var normalizedNames = roleNames.Select(roleName => roleName.ToUpperInvariant()).ToList();

    var scopes = await _appDbContext.RoleClaims
      .Join(
        _appDbContext.Roles.Where(role => normalizedNames.Contains(role.NormalizedName!)),
        roleClaim => roleClaim.RoleId,
        role => role.Id,
        (roleClaim, _) => roleClaim.ClaimValue!)
      .Distinct()
      .ToListAsync(cancellationToken);

    return [.. scopes];
  }
}
