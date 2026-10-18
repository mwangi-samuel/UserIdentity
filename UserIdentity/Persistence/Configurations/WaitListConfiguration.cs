using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PolyzenKit.Persistence.Configurations;

using UserIdentity.Domain.WaitLists;

namespace UserIdentity.Persistence.Configurations;

public class WaitListConfiguration : IEntityTypeConfiguration<WaitListEntity>
{
  public void Configure(EntityTypeBuilder<WaitListEntity> builder)
  {
    builder.ConfigureLongPrimaryKey();

    builder.HasIndex(e => e.UserEmail);
  }
}
