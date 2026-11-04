namespace Kazo.Bar.Infrastructure.Secrets;

/// <summary>Encrypts bytes for the current user. DPAPI on Windows; a fake in tests.</summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] plain);

    /// <exception cref="System.Security.Cryptography.CryptographicException">The data was not encrypted by this user.</exception>
    byte[] Unprotect(byte[] encrypted);
}
