using PolyzenKit.Application.Core;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record EmailChangeViewModel : ItemDetailBaseViewModel
{
    public required UserDto User { get; init; }

    public string? CodeSentTo { get; init; }
}
