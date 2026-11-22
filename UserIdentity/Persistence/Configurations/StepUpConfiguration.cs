using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Configurations;
using UserIdentity.Domain.StepUps;

namespace UserIdentity.Persistence.Configurations;

public class StepUpConfiguration : IEntityTypeConfiguration<StepUpEntity>
{
    public void Configure(EntityTypeBuilder<StepUpEntity> builder)
    {
        builder.ConfigureGuidPrimaryKey();

        builder.HasIndex(e => e.Ticket).IsUnique();

        builder.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RegisteredAppEntity>().WithMany().HasForeignKey(e => e.AppId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.UserId, e.AppId });
        builder.HasIndex(e => e.Expires);
    }
}
