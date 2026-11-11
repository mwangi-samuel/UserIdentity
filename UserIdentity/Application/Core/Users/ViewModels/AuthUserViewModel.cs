using PolyzenKit.Application.Core;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record AuthUserViewModel : ItemDetailBaseViewModel
{
    public required UserDto User { get; init; }
    public bool IsConfirmed { get; init; }
    public UserTokenDto? UserToken { get; init; }
    public string? LoginTicket { get; init; }
}
