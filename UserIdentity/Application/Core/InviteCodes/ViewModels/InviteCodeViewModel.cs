using PolyzenKit.Application.Core;

using UserIdentity.Domain.InviteCodes;

namespace UserIdentity.Application.Core.InviteCodes.ViewModels;

public record InviteCodeViewModel : ItemDetailBaseViewModel
{
  public required InviteCodeDto InviteCode { get; init; }
}
