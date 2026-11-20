namespace UserIdentity.Common.Enums;

public enum CodeCheckOutcome
{
    Verified,
    WrongCode,
    Expired,
    NoAttemptsLeft,
    NotFound,
}
