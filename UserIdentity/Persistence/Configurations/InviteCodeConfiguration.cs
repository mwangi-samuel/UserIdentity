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

    builder.Property(e => e.InviteCode).HasMaxLength(InviteCodeEntity.InviteCodeMaxLength);
    builder.Property(e => e.UserEmail).HasMaxLength(InviteCodeEntity.UserEmailMaxLength);
    builder.Property(e => e.Role).HasMaxLength(InviteCodeEntity.RoleMaxLength);
    builder.HasIndex(e => new { e.AppId, e.UserEmail }).IsUnique();
  }
}
