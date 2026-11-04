namespace Kazo.Bar.Infrastructure.Secrets;

using System.Text.RegularExpressions;

/// <summary>Validation of <c>secretRef</c> values used as keys in every secret store.</summary>
public static partial class SecretReference
{
    /// <summary>Returns the reference, or throws when it is not 1-100 letters, digits, '.', '-' or '_'.</summary>
    public static string Validate(string secretRef)
    {
        if (string.IsNullOrEmpty(secretRef) || !Pattern().IsMatch(secretRef))
            throw new ArgumentException("A secret reference uses 1-100 letters, digits, '.', '-' or '_'.", nameof(secretRef));
        return secretRef;
    }

    /// <summary>True when <see cref="Validate"/> would accept the reference.</summary>
    public static bool IsValid(string? secretRef) => !string.IsNullOrEmpty(secretRef) && Pattern().IsMatch(secretRef);

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex Pattern();
}
