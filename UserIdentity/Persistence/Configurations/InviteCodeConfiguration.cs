using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PolyzenKit.Persistence.Configurations;

using UserIdentity.Domain.InviteCodes;

namespace UserIdentity.Persistence.Configurations;

public class InviteCodeConfiguration : IEntityTypeConfiguration<InviteCodeEntity>
{
  public void Configure(EntityTypeBuilder<InviteCodeEntity> builder)
  {
    builder.ConfigureLongPrimaryKey();
  }
}
