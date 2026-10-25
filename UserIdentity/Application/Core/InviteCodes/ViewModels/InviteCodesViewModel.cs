using PolyzenKit.Application.Core;

using UserIdentity.Domain.InviteCodes;

namespace UserIdentity.Application.Core.InviteCodes.ViewModels;

public record InviteCodesViewModel : ItemsBaseViewModel
{
  public required List<InviteCodeDto> InviteCodes { get; init; }

  public required int TotalCount { get; init; }
}
