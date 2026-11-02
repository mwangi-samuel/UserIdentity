using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UserIdentity.Persistence.Configurations;

// Applied after Identity's own model, whose email index isn't unique
public class IdentityUserConfiguration : IEntityTypeConfiguration<IdentityUser>
{
  public void Configure(EntityTypeBuilder<IdentityUser> builder)
  {
    builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
  }
}
