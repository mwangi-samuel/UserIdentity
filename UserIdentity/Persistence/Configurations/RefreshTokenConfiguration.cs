using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PolyzenKit.Persistence.Configurations;

using UserIdentity.Domain.RefreshTokens;

namespace UserIdentity.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshTokenEntity>
{

  public void Configure(EntityTypeBuilder<RefreshTokenEntity> builder)
  {
    builder.ConfigureGuidPrimaryKey();

    builder.Property(e => e.Token).HasMaxLength(200);
    builder.HasIndex(e => e.Token);

    builder.Property(e => e.PreviousToken).HasMaxLength(200);
    builder.HasIndex(e => e.PreviousToken);

    builder.Property(e => e.UserId).HasMaxLength(50);
    builder.HasIndex(e => e.UserId);

    builder.Property(e => e.RemoteIpAddress).HasMaxLength(20);
  }
}
