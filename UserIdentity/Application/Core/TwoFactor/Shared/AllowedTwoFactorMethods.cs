using UserIdentity.Common.Enums;
using UserIdentity.Domain.AppSignInSettings;

namespace UserIdentity.Application.Core.TwoFactor.Shared;

public static class AllowedTwoFactorMethods
{
    public static List<TwoFactorMethod> For(AppSignInSettingsDto signInSettings)
    {
        List<TwoFactorMethod> methods = [];

        if (signInSettings.AllowAuthenticatorTwoFactor)
            methods.Add(TwoFactorMethod.Authenticator);

        if (signInSettings.AllowEmailTwoFactor)
            methods.Add(TwoFactorMethod.Email);

        if (signInSettings.AllowSmsTwoFactor)
            methods.Add(TwoFactorMethod.Sms);

        return methods;
    }

    public static TwoFactorMethod? InUse(AppSignInSettingsDto signInSettings, TwoFactorMethod? method) =>
        method is { } userMethod && For(signInSettings).Contains(userMethod) ? userMethod : null;
}
