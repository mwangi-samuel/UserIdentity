using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record VerifyPasswordResetViewModel : ItemDetailBaseViewModel
{
  public required VerifyPasswordResetDTO VerifyPasswordResetResult { get; init; }
}
