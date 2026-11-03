using Microsoft.EntityFrameworkCore;

using MySqlConnector;

using PolyzenKit.Persistence.Infrastructure;
using PolyzenKit.Persistence.Settings;

namespace UserIdentity.Persistence;

public static class AppDbContextOptions
{
  public static DbContextOptionsBuilder UseAppMysql(
    this DbContextOptionsBuilder options,
    MysqlSettings mysqlSettings) =>
    options
      .UseMySql(mysqlSettings.ConnectionString, ServerVersion.AutoDetect(mysqlSettings.ConnectionString))
      .UseSnakeCaseNamingConvention()
      .AddInterceptors(new UniqueViolationInterceptor(exception => exception is MySqlException { Number: 1062 }));
}
