using PolyzenKit.Common.Utilities;

namespace UserIdentity.Common.Validation;

public static class ValidationPatterns
{
    public const string Email = @"^[^\s@]+@[^\s@]+$";

    public const string ScopeAction =
        $"(?i)^({ScopeActions.Create}|{ScopeActions.Read}|{ScopeActions.Edit}|{ScopeActions.Delete}|{ScopeActions.Download}|{ScopeActions.All})$";
}
