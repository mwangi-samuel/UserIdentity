using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Persistence.Configurations;

public class IdentityRoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    public void Configure(EntityTypeBuilder<IdentityRole> builder)
    {
        builder.Property(role => role.Name).HasMaxLength(RoleLimits.NameMaxLength);
        builder.Property(role => role.NormalizedName).HasMaxLength(RoleLimits.NameMaxLength);
    }
}
