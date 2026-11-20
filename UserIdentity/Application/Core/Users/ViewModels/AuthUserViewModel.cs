using PolyzenKit.Application.Core;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record AuthUserViewModel : ItemDetailBaseViewModel
{
    public required UserDto User { get; init; }

    public required SignInStep NextStep { get; init; }

    public UserTokenDto? UserToken { get; init; }

    public string? Ticket { get; init; }

    public TwoFactorMethod? Method { get; init; }

    public List<TwoFactorMethod>? AllowedMethods { get; init; }

    public string? CodeSentTo { get; init; }

    public List<string>? RecoveryCodes { get; init; }
}
