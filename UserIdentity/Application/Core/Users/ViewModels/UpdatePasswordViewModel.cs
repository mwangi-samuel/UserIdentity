using PolyzenKit.Application.Core;
using UserIdentity.Application.Core.Tokens.ViewModels;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record UpdatePasswordViewModel : BaseViewModel
{
    public required UpdatePasswordDto UpdatePasswordResult { get; init; }
    public UserTokenDto? UserToken { get; init; }
}
