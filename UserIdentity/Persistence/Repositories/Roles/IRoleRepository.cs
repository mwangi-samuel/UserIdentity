using UserIdentity.Domain.Roles;

namespace UserIdentity.Persistence.Repositories.Roles;

public interface IRoleRepository
{
  Task<List<RoleDto>> GetRoleDtosAsync(string? namePrefix, CancellationToken cancellationToken = default);

  Task<List<string>> GetUserRoleNamesAsync(string userId, CancellationToken cancellationToken = default);

  Task<HashSet<string>> GetScopesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default);
}
