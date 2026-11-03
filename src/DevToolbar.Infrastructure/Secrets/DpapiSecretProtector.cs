namespace DevToolbar.Infrastructure.Secrets;

using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Windows DPAPI, current-user scope: only the same Windows account on the same machine
/// (or its roaming profile) can decrypt. A copied <c>secrets.json</c> is useless elsewhere.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    /// <summary>Extra entropy so other applications using DPAPI for this user cannot read DevToolbar's values by accident.</summary>
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DevToolbar.Secrets.v1");

    public byte[] Protect(byte[] plain) => ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] encrypted) => ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
}
