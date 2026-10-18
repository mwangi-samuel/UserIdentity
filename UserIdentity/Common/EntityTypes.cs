using System.ComponentModel;

namespace UserIdentity.Common;

public enum EntityTypes
{
  [Description("User")]
  USER,

  [Description("Role")]
  ROLE,

  [Description("RoleClaim")]
  ROLE_CLAIM,

  [Description("RefreshToken")]
  REFRESH_TOKEN,

  [Description("InviteCode")]
  INVITE_CODE,

  [Description("WaitList")]
  WAIT_LIST,

  [Description("PasswordReset")]
  PASSWORD_RESET
}
