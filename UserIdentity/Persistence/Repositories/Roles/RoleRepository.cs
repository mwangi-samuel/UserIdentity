using Microsoft.EntityFrameworkCore;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Persistence.Repositories.Roles;

public class RoleRepository(AppDbContext appDbContext) : IRoleRepository
{
    private readonly AppDbContext _appDbContext = appDbContext;

    public async Task<List<RoleDto>> GetRoleDtosAsync(string? namePrefix, CancellationToken cancellationToken = default)
    {
        var normalizedPrefix = namePrefix?.ToUpperInvariant();

        return await _appDbContext
            .Roles.Where(role => normalizedPrefix == null || role.NormalizedName!.StartsWith(normalizedPrefix))
            .OrderBy(role => role.Name)
            .ThenBy(role => role.Id)
            .Select(role => new RoleDto { Id = role.Id, Name = role.Name! })
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<string, HashSet<string>>> GetUserRoleScopesAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var roleScopes = await _appDbContext
            .UserRoles.Where(userRole => userRole.UserId == userId)
            .Join(_appDbContext.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role)
            .LeftJoin(
                _appDbContext.RoleClaims,
                role => role.Id,
                roleClaim => roleClaim.RoleId,
                (role, roleClaim) => new { RoleName = role.Name!, Scope = roleClaim == null ? null : roleClaim.ClaimValue }
            )
            .ToListAsync(cancellationToken);

        return roleScopes
            .GroupBy(roleScope => roleScope.RoleName)
            .ToDictionary(
                group => group.Key,
                group => group.Where(roleScope => roleScope.Scope is not null).Select(roleScope => roleScope.Scope!).ToHashSet()
            );
    }
}
