namespace UserIdentity.IntegrationTests.TestUtils;

internal static class UserSettingHelper
{
  public static string UserId = Guid.CreateVersion7().ToString();
  public const string UserEmail = "test.user@domain.com";
  public const string UserName = "test.user";
  public const string FirstName = "Test";
  public const string LastName = "User";
  public const string PhoneNumber = "0123456789";
  public const string UserPassword = "Password123+";
  public const string InvalidUserToken = "eyJhbGciOiJIUzI1NiIsImtpZCI6IlFWQlFYMHRGV1Y5SlJBIiwidHlwIjoiSldUIiwiY3R5IjoiSldUIn0.eyJzdWIiOiJ0ZXN0LnVzZXIiLCJqdGkiOiJjNmRhNjY4Yy0xMzEzLTRhOTItOTQzYy1hMzVlMjA3MTYwZWEiLCJpYXQiOjE2Nzg3MDg0NzcsImlkIjoiNWZhODk4MjAtZTViMS00NmY2LWEwZmItYmQwMTk2NGY2NzRmIiwicm9sZXMiOiJBZG1pbmlzdHJhdG9yIiwibmJmIjoxNjc4NzA4NDc3LCJleHAiOjE2Nzg3MDg0OTIsImlzcyI6IklOVkFMSURfSVNTVUVSIiwiYXVkIjoiQVBQX0FVRElFTkNFIn0.Bcyy1ty8Uqa6duR8NrVolF4DlCfkKptWYP_oVwDVC5k";
}
