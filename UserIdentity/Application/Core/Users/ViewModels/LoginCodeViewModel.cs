using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record LoginCodeViewModel : BaseViewModel
{
    public bool Sent { get; init; }

    public string? CodeSentTo { get; init; }
}
