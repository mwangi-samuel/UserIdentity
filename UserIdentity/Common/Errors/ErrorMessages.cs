namespace UserIdentity.Common.Errors;

public static class ErrorMessages
{
  public static class Users
  {
    public const string PhoneNumberOrEmailRequired = "Either PhoneNumber or UserEmail must be provided.";
    public const string InvalidRecaptchaToken = "Invalid GoogleRecaptchaToken provided";
    public const string EmailRequiredForInviteCode = "An email is required when signing up with an invite code.";
    public const string InviteCodeRequired = "A valid invite code is required when signing up.";

    public static string InviteRoleMissing(string role) => $"The role {role} granted by the invite code doesn't exist.";
  }

  public static class Roles
  {
    public const string GlobalAdminRoleScopes = "Scopes can't be added to the global admin role";

    public static string NameOutsideApp(string appPrefix) => $"Role names must start with {appPrefix}";
  }

  public static class InviteCodes
  {
    public const string UsedInviteNotRevocable = "An invite code that has been used can't be revoked";

    public static string RoleNotGrantable(
      string appPrefix) => $"An invite can only grant a {appPrefix} role that isn't an admin role";

    public static string RoleMissing(string role) => $"The role {role} doesn't exist";
  }
}
