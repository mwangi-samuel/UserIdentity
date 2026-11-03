using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;

using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Persistence.Repositories.Roles;

namespace UserIdentity.Application.Core.Roles.Queries;

public record GetRolesQuery : IBaseQuery
{
  public string? AppName { get; init; }
}

public class GetRolesQueryHandler(IRoleRepository roleRepository) : IGetItemsQueryHandler<GetRolesQuery, RolesViewModel>
{
  private readonly IRoleRepository _roleRepository = roleRepository;

  public async Task<RolesViewModel> GetItemsAsync(GetRolesQuery query, CancellationToken cancellationToken = default)
  {
    var namePrefix = query.AppName is null ? null : AppRoleNames.Prefix(query.AppName);

    return new RolesViewModel { Roles = await _roleRepository.GetRoleDtosAsync(namePrefix, cancellationToken) };
  }
}
