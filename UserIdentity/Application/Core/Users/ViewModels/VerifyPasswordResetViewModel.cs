using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record VerifyPasswordResetViewModel : ItemDetailBaseViewModel
{
  public required VerifyPasswordResetDto VerifyPasswordResetResult { get; init; }
}
