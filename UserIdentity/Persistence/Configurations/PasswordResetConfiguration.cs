using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PolyzenKit.Persistence.Configurations;

using UserIdentity.Domain.PasswordResets;

namespace UserIdentity.Persistence.Configurations;

public class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordResetEntity>
{
  public void Configure(EntityTypeBuilder<PasswordResetEntity> builder)
  {
    builder.ConfigureGuidPrimaryKey();

    builder.Property(e => e.RequestId).HasMaxLength(200);
    builder.HasIndex(e => e.RequestId).IsUnique();

    builder.Property(e => e.UserId).HasMaxLength(50);
    builder.HasIndex(e => e.UserId);
  }
}
