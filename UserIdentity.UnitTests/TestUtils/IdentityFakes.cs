using FakeItEasy;

using Microsoft.AspNetCore.Identity;

namespace UserIdentity.UnitTests.TestUtils;

internal static class IdentityFakes
{
  // Since .NET 10 the UserManager constructor resolves IMeterFactory from its IServiceProvider argument.
  // FakeItEasy's dummy provider returns an object that fails that cast, so pass the arguments explicitly.
  internal static UserManager<IdentityUser> UserManager() =>
    A.Fake<UserManager<IdentityUser>>(options => options.WithArgumentsForConstructor(
      () => new UserManager<IdentityUser>(A.Fake<IUserStore<IdentityUser>>(), null!, null!, null!, null!, null!, null!, null!, null!)));
}
