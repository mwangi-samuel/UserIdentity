using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolyzenKit.Persistence.Configurations;
using UserIdentity.Domain.InviteCodes;

namespace UserIdentity.Persistence.Configurations;

public class InviteCodeConfiguration : IEntityTypeConfiguration<InviteCodeEntity>
{
    public void Configure(EntityTypeBuilder<InviteCodeEntity> builder)
    {
        builder.ConfigureLongPrimaryKey();

        builder.Property(e => e.InviteCode).HasMaxLength(InviteCodeLimits.InviteCodeMaxLength);
        builder.Property(e => e.UserEmail).HasMaxLength(InviteCodeLimits.UserEmailMaxLength);
        builder.Property(e => e.Role).HasMaxLength(InviteCodeLimits.RoleMaxLength);
        builder.HasIndex(e => new { e.AppId, e.UserEmail }).IsUnique();
    }
}
