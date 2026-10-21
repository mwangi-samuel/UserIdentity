using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using UserIdentity.Domain.Users;

namespace UserIdentity.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
  public void Configure(EntityTypeBuilder<UserEntity> builder)
  {
    builder.HasKey(e => e.Id);
    builder.HasIndex(e => e.CreatedAt);

    builder.Property(e => e.EmailConfirmationToken).HasMaxLength(600);

    builder.Property(e => e.FirstName).HasMaxLength(20);

    builder.Property(e => e.LastName).HasMaxLength(20);
  }
}
