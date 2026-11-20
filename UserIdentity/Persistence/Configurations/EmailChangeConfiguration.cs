using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolyzenKit.Persistence.Configurations;
using UserIdentity.Domain.EmailChanges;

namespace UserIdentity.Persistence.Configurations;

public class EmailChangeConfiguration : IEntityTypeConfiguration<EmailChangeEntity>
{
    public void Configure(EntityTypeBuilder<EmailChangeEntity> builder)
    {
        builder.ConfigureGuidPrimaryKey();

        builder.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.UserId).IsUnique();
        builder.HasIndex(e => e.Expires);
    }
}
