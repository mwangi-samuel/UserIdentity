using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace UserIdentity.UnitTests.Persistence.Configurations;

internal static class EntityConfigurationPropertiesTestsHelper
{

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Unit Tests")]
  public static bool ConfirmMaxColumnLength(this EntityType entityType, string column, int columnLength)
  {
    var properies = entityType.GetProperties();

    foreach (var prop in properies)
    {
      if (prop.Name == column)
      {
        return prop.GetMaxLength() == columnLength;
      }
    }

    return false;
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Unit Tests")]
  public static bool ConfirmColumnHasIndex(this EntityType entityType, string column)
  {
    var properies = entityType.GetProperties();

    foreach (var prop in properies)
    {
      if (prop.Name == column)
      {
        return prop.IsIndex();
      }
    }

    return false;
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Unit Tests")]
  public static bool ConfirmCascadingForeignKey<TPrincipal>(this EntityType entityType, string column) =>
    entityType.GetForeignKeys().Any(foreignKey =>
      foreignKey.Properties.Any(property => property.Name == column)
      && foreignKey.PrincipalEntityType.ClrType == typeof(TPrincipal)
      && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Unit Tests")]
  public static bool ConfirmColumnHasKey(this EntityType entityType, string column)
  {
    var properies = entityType.GetProperties();

    foreach (var prop in properies)
    {
      if (prop.Name == column)
      {
        return prop.IsKey();
      }
    }

    return false;
  }
}
