using PolyzenKit.Common.Utilities;

namespace UserIdentity.UnitTests.TestUtils;

internal static class TestStringHelper
{
  internal static string UserId = RandomStrings.Generate(32);
  internal static string UserName = RandomStrings.Generate(40);
}
