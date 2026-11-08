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
        var roles = AppRoleNames.ForApp(
            await _roleRepository.GetUserRoleNamesAsync(command.UserId, cancellationToken),
            command.AppName,
            _roleSettings.GlobalAdminRole
        );
        var scopes = await _roleRepository.GetScopesAsync(roles, cancellationToken);

        (var token, var expiresIn) = _jwtTokenHandler.CreateToken(command.UserId, command.UserName, command.AppName, [.. roles], scopes);

        return new AccessTokenViewModel
        {
            AccessToken = new AccessTokenDto { Token = token, ExpiresIn = expiresIn },
        };
    }
}
