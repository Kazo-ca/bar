namespace Kazo.Bar.Infrastructure.Secrets;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Kazo.Bar.Core.Interfaces;

/// <summary>
/// Secrets as generic credentials in the Windows Credential Manager (US2.4), named
/// <c>Kazo.Bar:{secretRef}</c>. They are visible (not readable) in
/// Control Panel, Credential Manager, Windows Credentials. Secrets saved before the
/// Kazo Bar rename, under <c>DevToolbar:{secretRef}</c>, are moved by
/// <see cref="MigrateLegacySecrets"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CredentialManagerSecretStore : ISecretStore
{
    public const string TargetPrefix = "Kazo.Bar:";

    /// <summary>Prefix used before the Kazo Bar rename.</summary>
    public const string LegacyTargetPrefix = "DevToolbar:";

    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    /// <summary>Credential Manager limit for a generic credential blob.</summary>
    private const int MaxBlobBytes = 5 * 512;

    /// <summary>Raw generic-credential access, replaced in tests.</summary>
    internal interface ICredentialApi
    {
        string? Read(string target, string secretRef);
        void Write(string target, string secretRef, string secret);
        bool Delete(string target, string secretRef);
        IReadOnlyList<string> Enumerate(string prefix);
    }

    private readonly ICredentialApi _api;

    public CredentialManagerSecretStore() : this(new NativeCredentialApi()) { }

    internal CredentialManagerSecretStore(ICredentialApi api) => _api = api;

    public Task SetAsync(string secretRef, string secret)
    {
        SecretReference.Validate(secretRef);
        ArgumentNullException.ThrowIfNull(secret);
        _api.Write(TargetPrefix + secretRef, secretRef, secret);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Moves every <c>DevToolbar:{ref}</c> credential to <c>Kazo.Bar:{ref}</c>. A legacy
    /// credential is deleted only once its value is saved under the new name; when both exist,
    /// the new one wins and the legacy one is removed. Returns the number of secrets moved.
    /// </summary>
    public int MigrateLegacySecrets()
    {
        var moved = 0;
        foreach (var secretRef in _api.Enumerate(LegacyTargetPrefix))
        {
            if (!SecretReference.IsValid(secretRef))
                continue;
            var legacyTarget = LegacyTargetPrefix + secretRef;
            var value = _api.Read(legacyTarget, secretRef);
            if (value is null)
                continue;
            if (_api.Read(TargetPrefix + secretRef, secretRef) is null)
            {
                _api.Write(TargetPrefix + secretRef, secretRef, value);
                moved++;
            }
            _api.Delete(legacyTarget, secretRef);
        }
        return moved;
    }

    public Task<string?> GetAsync(string secretRef)
    {
        SecretReference.Validate(secretRef);
        return Task.FromResult(_api.Read(TargetPrefix + secretRef, secretRef));
    }

    public Task<bool> DeleteAsync(string secretRef)
    {
        SecretReference.Validate(secretRef);
        return Task.FromResult(_api.Delete(TargetPrefix + secretRef, secretRef));
    }

    public Task<IReadOnlyList<string>> ListAsync() => Task.FromResult(_api.Enumerate(TargetPrefix));

    private sealed class NativeCredentialApi : ICredentialApi
    {
        public string? Read(string target, string secretRef) => CredentialManagerSecretStore.Read(target, secretRef);
        public void Write(string target, string secretRef, string secret) => CredentialManagerSecretStore.Write(target, secretRef, secret);
        public bool Delete(string target, string secretRef) => CredentialManagerSecretStore.Delete(target, secretRef);
        public IReadOnlyList<string> Enumerate(string prefix) => CredentialManagerSecretStore.Enumerate(prefix);
    }

    private static void Write(string target, string secretRef, string secret)
    {
        var blob = Encoding.UTF8.GetBytes(secret);
        if (blob.Length > MaxBlobBytes)
            throw new ArgumentException($"The secret is longer than the {MaxBlobBytes} bytes Credential Manager accepts.", nameof(secret));

        var handle = GCHandle.Alloc(blob, GCHandleType.Pinned);
        try
        {
            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                UserName = Environment.UserName,
                Comment = "Kazo Bar access token",
                CredentialBlobSize = blob.Length,
                CredentialBlob = handle.AddrOfPinnedObject(),
                Persist = CredPersistLocalMachine,
            };
            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not save the secret '{secretRef}' in Credential Manager.");
        }
        finally
        {
            Array.Clear(blob);
            handle.Free();
        }
    }

    private static string? Read(string target, string secretRef)
    {
        if (!CredRead(target, CredTypeGeneric, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return null;
            throw new Win32Exception(error, $"Could not read the secret '{secretRef}' from Credential Manager.");
        }
        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                return string.Empty;
            var blob = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            var value = Encoding.UTF8.GetString(blob);
            Array.Clear(blob);
            return value;
        }
        finally
        {
            CredFree(pointer);
        }
    }

    private static bool Delete(string target, string secretRef)
    {
        if (CredDelete(target, CredTypeGeneric, 0))
            return true;
        var error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
            return false;
        throw new Win32Exception(error, $"Could not delete the secret '{secretRef}' from Credential Manager.");
    }

    private static IReadOnlyList<string> Enumerate(string prefix)
    {
        if (!CredEnumerate(prefix + "*", 0, out var count, out var list))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return [];
            throw new Win32Exception(error, "Could not list Kazo Bar secrets in Credential Manager.");
        }
        try
        {
            var references = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(Marshal.ReadIntPtr(list, i * IntPtr.Size));
                if (credential.TargetName?.StartsWith(prefix, StringComparison.Ordinal) == true)
                    references.Add(credential.TargetName[prefix.Length..]);
            }
            return references;
        }
        finally
        {
            CredFree(list);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredEnumerate(string filter, int flags, out int count, out IntPtr credentials);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
