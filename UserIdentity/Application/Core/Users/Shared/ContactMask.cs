namespace UserIdentity.Application.Core.Users.Shared;

public static class ContactMask
{
    private const string Mask = "***";
    private const int VisiblePhoneDigits = 3;

    public static string? For(string? email, string? phoneNumber) =>
        !string.IsNullOrWhiteSpace(email) ? Email(email)
        : !string.IsNullOrWhiteSpace(phoneNumber) ? PhoneNumber(phoneNumber)
        : null;

    public static string Email(string email)
    {
        var atIndex = email.LastIndexOf('@');

        return atIndex <= 0 ? Mask : $"{email[0]}{Mask}{email[atIndex..]}";
    }

    public static string PhoneNumber(string phoneNumber) =>
        phoneNumber.Length <= VisiblePhoneDigits ? Mask : $"{Mask}{phoneNumber[^VisiblePhoneDigits..]}";
}
