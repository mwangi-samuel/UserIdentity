using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace UserIdentity.UnitTests.Persistence.Configurations;

internal class EntityConfigurationTestsHelper<T> where T : class
{

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Unit Tests")]
  public static (EntityTypeBuilder<T>, EntityType) GetEntityTypeBuilder()
  {
    var enityType = new EntityType(nameof(T), typeof(T), new Model(), false, ConfigurationSource.Convention);
    var builder = new EntityTypeBuilder<T>(enityType);
    return (builder, enityType);
  }
}
