using Microsoft.Extensions.Configuration;
using PolyzenKit.Common.Exceptions;

namespace UserIdentity.IntegrationTests.TestUtils;

internal static class ConfigSettingHelper
{
    public static T GetSetting<T>(this IConfiguration configuration) =>
        configuration.GetSection(typeof(T).Name).Get<T>() ?? throw new MissingConfigurationException(typeof(T).Name);
}
