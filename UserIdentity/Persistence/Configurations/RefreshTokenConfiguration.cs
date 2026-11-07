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

        builder.Property(e => e.Token).HasMaxLength(RefreshTokenLimits.TokenMaxLength);
        builder.HasIndex(e => e.Token);

        builder.Property(e => e.PreviousToken).HasMaxLength(RefreshTokenLimits.PreviousTokenMaxLength);
        builder.HasIndex(e => e.PreviousToken);

        builder.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.UserId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.Expires);

        builder.Property(e => e.Version).IsConcurrencyToken();
    }
}
