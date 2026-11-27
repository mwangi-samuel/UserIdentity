namespace UserIdentity.Common.Errors;

public static class ErrorMessages
{
    public static class Users
    {
        public const string PhoneNumberOrEmailRequired = "Either PhoneNumber or UserEmail must be provided.";
        public const string InvalidEmail = "UserEmail must be a valid email address.";
        public const string UserNameCannotBeAnotherEmail = "A user name can only look like an email address when it matches UserEmail.";
        public const string EmailRequiredForInviteCode = "An email is required when signing up with an invite code.";
        public const string InviteCodeRequired = "A valid invite code is required when signing up.";
        public const string AccountNotCreated = "We couldn't create this account.";
        public const string TicketOrResetRequestIdRequired = "Send either ticket or resetRequestId, not both.";
        public const string WrongPassword = "That password isn't right.";
        public const string AccountLocked = "Too many failed attempts. Try again later.";
        public const string PasswordRequired = "Your password is required to change your email.";
        public const string EmailNotChanged = "The new email is the same as the current one.";
        public const string EmailNotChangedByAdmin = "We couldn't change this email.";
        public const string PhoneUsedForTwoFactor =
            "This phone number receives your two-step sign-in codes. Change your two-step sign-in method first.";
        public const string TooManyCodes = "Too many codes were sent today. Try again tomorrow.";
        public const string PasswordRequiredToDeleteAccount = "Your password is required to delete your account.";
        public const string LastGlobalAdmin = "This is the only global admin account, so it can't be deleted.";

        public static string InviteRoleMissing(string role) => $"The role {role} granted by the invite code doesn't exist.";

        public static string BootstrapAdminIncomplete(IEnumerable<string> missingSettings) =>
            $"The bootstrap admin also needs {string.Join(", ", missingSettings)}.";
    }

    public static class Codes
    {
        public const string NotAccepted = "That code didn't work. Check it, or send a new code.";
    }

    public static class TwoFactor
    {
        public const string CodeOrRecoveryCodeRequired = "Send either code or recoveryCode, not both.";
        public const string MethodNotAllowed = "That two-step sign-in method isn't available.";
        public const string PhoneNumberRequired = "A phone number is required for text messages.";
        public const string ChooseMethodFirst = "Choose a two-step sign-in method first.";
        public const string CodeRequired = "Enter the code to finish setting up two-step sign-in.";
        public const string Required = "Two-step sign-in is required for this account, so it can't be turned off.";
        public const string NotOn = "Two-step sign-in isn't on for this account.";
        public const string SetUpToConfirm = "Set up two-step sign-in to confirm this.";
        public const string ConfirmFirst = "Confirm with your sign-in code first.";
    }

    public static class SignInSettings
    {
        public const string DefaultRoleFormat = "The {0} field must be a role name without the app prefix, spaces or colons.";
        public const string TwoFactorWithoutMethod = "Allow at least one two-step sign-in method before requiring two-step sign-in.";
        public const string StaleVersion = "The sign-in settings' Version is not the current one.";

        public static string AdminDefaultRole(string defaultRole) =>
            $"The default role {defaultRole} is an admin role, which sign-up can't grant.";
    }

    public static class Roles
    {
        public const string GlobalAdminRoleScopes = "Scopes can't be added to the global admin role";
        public const string GrantedByUnusedInvites = "A role that unused invite codes grant can't be deleted";
        public const string InvalidScopeAction = "Action must be create, read, edit, delete, download or all";
        public const string LastGlobalAdminRole = "This is the only global admin account, so its global admin role can't be taken away.";

        public static string NameOutsideApp(string appPrefix) => $"Role names must start with {appPrefix}";
    }

    public static class InviteCodes
    {
        public const string UsedInviteNotRevocable = "An invite code that has been used can't be revoked";

        public static string RoleNotGrantable(string appPrefix) => $"An invite can only grant a {appPrefix} role that isn't an admin role";

        public static string RoleMissing(string role) => $"The role {role} doesn't exist";
    }
}
