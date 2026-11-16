using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.RegisteredApps.Shared;

namespace UserIdentity.Application.Core.Users.Shared;

public static class AccountConfirmation
{
    public static bool IsConfirmed(ActiveRegisteredApp registeredApp, IdentityUser user) =>
        !registeredApp.RequireConfirmedAccount || HasConfirmedContact(user);

    public static bool HasConfirmedContact(IdentityUser user) => user.EmailConfirmed || user.PhoneNumberConfirmed;
}
