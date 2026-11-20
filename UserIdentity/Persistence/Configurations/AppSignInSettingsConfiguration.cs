using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Configurations;
using UserIdentity.Domain.AppSignInSettings;

namespace UserIdentity.Persistence.Configurations;

public class AppSignInSettingsConfiguration : IEntityTypeConfiguration<AppSignInSettingsEntity>
{
    public void Configure(EntityTypeBuilder<AppSignInSettingsEntity> builder)
    {
        builder.ConfigureGuidPrimaryKey();

        builder.HasOne<RegisteredAppEntity>().WithMany().HasForeignKey(e => e.AppId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.AppId).IsUnique();
    }
}
