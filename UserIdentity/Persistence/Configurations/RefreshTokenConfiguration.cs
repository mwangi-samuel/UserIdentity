using Microsoft.AspNetCore.Identity;
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

        builder.HasIndex(e => e.Token).IsUnique();
        builder.HasIndex(e => e.PreviousToken).IsUnique();

        builder.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.Expires);
    }
}
