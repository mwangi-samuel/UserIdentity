using System.Linq.Expressions;

using PolyzenKit.Domain.Dto;

namespace UserIdentity.Domain.InviteCodes;

public class InviteCodeDto : InviteCodeEntity, IBaseAuditableEntityDto<long>
{
  public string? AppName { get; set; }

  public static Expression<Func<InviteCodeEntity, InviteCodeDto>> Basic
    => entity => new InviteCodeDto
    {
      Id = entity.Id,
      InviteCode = entity.InviteCode,
      UserEmail = entity.UserEmail,
      AppId = entity.AppId
    };

  private static readonly Func<InviteCodeEntity, InviteCodeDto> _compiledBasic
       = Basic.Compile();

  public static InviteCodeDto FromEntityBasic(InviteCodeEntity entity)
        => _compiledBasic(entity);

  public static Expression<Func<InviteCodeEntity, InviteCodeDto>> Detailed
    => entity => new InviteCodeDto
    {
      Id = entity.Id,
      InviteCode = entity.InviteCode,
      UserEmail = entity.UserEmail,
      AppId = entity.AppId,
      AppName = entity.App.AppName,
      CreatedBy = entity.CreatedBy,
      CreatedAt = entity.CreatedAt,
      UpdatedBy = entity.UpdatedBy,
      UpdatedAt = entity.UpdatedAt
    };

  private static readonly Func<InviteCodeEntity, InviteCodeDto> _compiledDetailed
        = Detailed.Compile();

  public static InviteCodeDto FromEntityDetailed(InviteCodeEntity entity)
        => _compiledDetailed(entity);
}
