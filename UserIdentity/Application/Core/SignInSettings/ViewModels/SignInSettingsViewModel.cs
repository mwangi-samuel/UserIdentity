using PolyzenKit.Application.Core;
using UserIdentity.Domain.AppSignInSettings;

namespace UserIdentity.Application.Core.SignInSettings.ViewModels;

public record SignInSettingsViewModel : ItemDetailBaseViewModel
{
    public required AppSignInSettingsDto SignInSettings { get; init; }
}
