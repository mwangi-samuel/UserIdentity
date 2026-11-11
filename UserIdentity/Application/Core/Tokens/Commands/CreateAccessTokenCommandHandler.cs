using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Settings;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Persistence.Repositories.Roles;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record CreateAccessTokenCommand(string UserId, string UserName, string AppName) : IBaseCommand;

public class CreateAccessTokenCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    IRoleRepository roleRepository,
    IJwtTokenHandler jwtTokenHandler
) : ICreateItemCommandHandler<CreateAccessTokenCommand, AccessTokenViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;

    public async Task<AccessTokenViewModel> CreateItemAsync(
        CreateAccessTokenCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var roleScopes = await _roleRepository.GetUserRoleScopesAsync(command.UserId, cancellationToken);
        var roles = AppRoleNames.ForApp(roleScopes.Keys, command.AppName, _roleSettings.GlobalAdminRole);
        HashSet<string> scopes = [.. roles.SelectMany(role => roleScopes[role])];

        (var token, var expiresIn) = _jwtTokenHandler.CreateToken(command.UserId, command.UserName, command.AppName, [.. roles], scopes);

        return new AccessTokenViewModel
        {
            AccessToken = new AccessTokenDto { Token = token, ExpiresIn = expiresIn },
        };
    }
}
