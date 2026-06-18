namespace TenantCore.Api.Infrastructure.Auth;

/// <summary>
/// BCrypt-based password hashing. BCrypt is deliberately slow and salts automatically, which is
/// what you want for password storage (unlike a fast hash such as SHA-256).
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    // Work factor 12 ≈ a good balance of security and login latency in 2026.
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Malformed/legacy hash — treat as a failed verification rather than a 500.
            return false;
        }
    }
}
