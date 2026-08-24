using System.Security.Cryptography;
using ResellerSystem.Server.Application.Security;

namespace ResellerSystem.Server.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing using .NET's built-in
/// System.Security.Cryptography.Rfc2898DeriveBytes — deliberately not
/// BCrypt/Argon2 from a third-party package: the built-in primitive is
/// free, requires zero extra dependencies, and is a NIST-recommended KDF.
/// If stronger memory-hard hashing (Argon2id) is wanted later, this is the
/// one place to change. Contract (IPasswordHasher) lives in
/// Server.Application; this is just the concrete implementation.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 210_000; // OWASP 2023 recommendation for PBKDF2-SHA256

    public (string Hash, string Salt) Hash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool Verify(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHashBytes = Convert.FromBase64String(hash);
        var actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
    }
}
