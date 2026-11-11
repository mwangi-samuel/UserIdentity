using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Application.Core.Roles.Queries;

public record GetRoleQuery : IBaseQuery
{
    public required string RoleId { get; init; }

    public string? AppName { get; init; }
}

public class GetRoleQueryHandler(RoleManager<IdentityRole> roleManager) : IGetItemQueryHandler<GetRoleQuery, RoleViewModel>
{
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;

    public async Task<RoleViewModel> GetItemAsync(GetRoleQuery query, CancellationToken cancellationToken = default)
    {
        var role = await AppRoleNames.GetAppRoleAsync(_roleManager, query.RoleId, query.AppName);

        return new RoleViewModel
        {
            Role = new RoleDto { Id = query.RoleId, Name = role.Name! },
        };
    }
}
