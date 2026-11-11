using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Domain.RoleClaims;

namespace UserIdentity.Application.Core.Roles.Queries;

public record GetRoleClaimsQuery : IBaseQuery
{
    public required string RoleId { get; init; }

    public string? AppName { get; init; }
}

public class GetRoleClaimsQueryHandler(RoleManager<IdentityRole> roleManager, IJwtTokenHandler jwtTokenHandler)
    : IGetItemsQueryHandler<GetRoleClaimsQuery, RoleClaimsViewModel>
{
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;

    public async Task<RoleClaimsViewModel> GetItemsAsync(GetRoleClaimsQuery query, CancellationToken cancellationToken = default)
    {
        var role = await AppRoleNames.GetAppRoleAsync(_roleManager, query.RoleId, query.AppName);

        var roleClaims = await _roleManager.GetClaimsAsync(role);

        var rolesCollection = new List<RoleClaimDto>();

        foreach (var roleClaim in roleClaims)
        {
            (var resource, var action) = _jwtTokenHandler.DecodeScopeClaim(roleClaim);

            RoleClaimDto claimDto = new()
            {
                Resource = resource,
                Action = action,
                Scope = roleClaim.Value,
            };

            rolesCollection.Add(claimDto);
        }

        return new RoleClaimsViewModel { RoleClaims = rolesCollection };
    }
}
