namespace UserIdentity.Application.Interfaces;

public interface IGoogleRecaptchaService
{
  Task<bool> VerifyTokenAsync(string token, CancellationToken cancellationToken = default);
}
