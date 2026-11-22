using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record UsersViewModel : ItemsBaseViewModel
{
    public required List<UserListItemDto> Users { get; init; }

    public required int TotalCount { get; init; }
}
