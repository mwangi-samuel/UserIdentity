using PolyzenKit.Application.Core;

using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record VerifyPasswordResetViewModel : ItemDetailBaseViewModel
{
  public required VerifyPasswordResetDTO VerifyPasswordResetResult { get; init; }
}
