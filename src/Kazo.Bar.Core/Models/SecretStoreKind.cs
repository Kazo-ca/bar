namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>Where access tokens are kept (US2.4). The user chooses in Settings.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SecretStoreKind>))]
public enum SecretStoreKind
{
    /// <summary>Windows Credential Manager (generic credentials, "DevToolbar:" prefix).</summary>
    CredentialManager,

    /// <summary>%APPDATA%\DevToolbar\secrets.json, each value encrypted for the current Windows user (DPAPI).</summary>
    EncryptedFile,
}
