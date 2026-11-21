using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record SessionsViewModel : ItemsBaseViewModel
{
    public required List<SessionDto> Sessions { get; init; }
}
