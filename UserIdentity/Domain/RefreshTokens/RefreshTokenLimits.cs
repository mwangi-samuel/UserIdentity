namespace UserIdentity.Domain.RefreshTokens;

public static class RefreshTokenLimits
{
    public const int TokenMaxLength = 200;
    public const int PreviousTokenMaxLength = 200;
    public const int IpAddressMaxLength = 64;
    public const int DeviceMaxLength = 256;
}
