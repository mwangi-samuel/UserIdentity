using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Configurations;
using UserIdentity.Domain.PasswordResets;

namespace UserIdentity.Persistence.Configurations;

public class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordResetEntity>
{
    public void Configure(EntityTypeBuilder<PasswordResetEntity> builder)
    {
        builder.ConfigureGuidPrimaryKey();

        builder.HasIndex(e => e.RequestId).IsUnique();

        builder.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RegisteredAppEntity>().WithMany().HasForeignKey(e => e.AppId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.Expires);
    }
}
