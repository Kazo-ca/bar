namespace Kazo.Bar.Infrastructure.Secrets;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Kazo.Bar.Core.Interfaces;

/// <summary>
/// Secrets as generic credentials in the Windows Credential Manager (US2.4), named
/// <c>DevToolbar:{secretRef}</c>. They are visible (not readable) in
/// Control Panel, Credential Manager, Windows Credentials.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CredentialManagerSecretStore : ISecretStore
{
    public const string TargetPrefix = "DevToolbar:";

    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    /// <summary>Credential Manager limit for a generic credential blob.</summary>
    private const int MaxBlobBytes = 5 * 512;

    public Task SetAsync(string secretRef, string secret)
    {
        SecretReference.Validate(secretRef);
        ArgumentNullException.ThrowIfNull(secret);
        var blob = Encoding.UTF8.GetBytes(secret);
        if (blob.Length > MaxBlobBytes)
            throw new ArgumentException($"The secret is longer than the {MaxBlobBytes} bytes Credential Manager accepts.", nameof(secret));

        var handle = GCHandle.Alloc(blob, GCHandleType.Pinned);
        try
        {
            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = TargetPrefix + secretRef,
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
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string secretRef)
    {
        SecretReference.Validate(secretRef);
        if (!CredRead(TargetPrefix + secretRef, CredTypeGeneric, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return Task.FromResult<string?>(null);
            throw new Win32Exception(error, $"Could not read the secret '{secretRef}' from Credential Manager.");
        }
        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                return Task.FromResult<string?>(string.Empty);
            var blob = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            var value = Encoding.UTF8.GetString(blob);
            Array.Clear(blob);
            return Task.FromResult<string?>(value);
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public Task<bool> DeleteAsync(string secretRef)
    {
        SecretReference.Validate(secretRef);
        if (CredDelete(TargetPrefix + secretRef, CredTypeGeneric, 0))
            return Task.FromResult(true);
        var error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
            return Task.FromResult(false);
        throw new Win32Exception(error, $"Could not delete the secret '{secretRef}' from Credential Manager.");
    }

    public Task<IReadOnlyList<string>> ListAsync()
    {
        if (!CredEnumerate(TargetPrefix + "*", 0, out var count, out var list))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return Task.FromResult<IReadOnlyList<string>>([]);
            throw new Win32Exception(error, "Could not list Kazo Bar secrets in Credential Manager.");
        }
        try
        {
            var references = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(Marshal.ReadIntPtr(list, i * IntPtr.Size));
                if (credential.TargetName?.StartsWith(TargetPrefix, StringComparison.Ordinal) == true)
                    references.Add(credential.TargetName[TargetPrefix.Length..]);
            }
            return Task.FromResult<IReadOnlyList<string>>(references);
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
