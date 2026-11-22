using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.TwoFactor.ViewModels;

public record StepUpUserViewModel : BaseViewModel
{
    public required IdentityUser User { get; init; }

    public required UserDto UserDetails { get; init; }

    public required TwoFactorMethod Method { get; init; }
}
