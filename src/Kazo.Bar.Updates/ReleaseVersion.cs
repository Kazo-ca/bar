namespace Kazo.Bar.Updates;

/// <summary>
/// A release version such as <c>v0.2.0</c>, <c>0.2.0-beta.1</c> or <c>0.2.0+abc123</c> (build metadata ignored).
/// A stable version is newer than any pre-release of the same numbers.
/// </summary>
public sealed record ReleaseVersion(Version Numbers, string? PreRelease) : IComparable<ReleaseVersion>
{
    public bool IsPreRelease => PreRelease != null;

    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V')) value = value[1..];
        var plus = value.IndexOf('+');
        if (plus >= 0) value = value[..plus];

        string? preRelease = null;
        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            preRelease = value[(dash + 1)..];
            value = value[..dash];
            if (preRelease.Length == 0) return false;
        }

        if (!Version.TryParse(value.Contains('.') ? value : value + ".0", out var numbers)) return false;
        version = new ReleaseVersion(Normalize(numbers), preRelease);
        return true;
    }

    public static ReleaseVersion Parse(string text) =>
        TryParse(text, out var version) ? version : throw new FormatException($"'{text}' is not a release version.");

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        var byNumbers = Numbers.CompareTo(other.Numbers);
        if (byNumbers != 0) return byNumbers;
        if (PreRelease == other.PreRelease) return 0;
        if (PreRelease == null) return 1;
        if (other.PreRelease == null) return -1;
        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) > 0;
    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) < 0;

    public override string ToString() =>
        PreRelease == null ? Numbers.ToString() : $"{Numbers}-{PreRelease}";

    /// <summary>1.2 → 1.2.0 and 1.2.3.0 → 1.2.3, so assembly versions compare equal to tags.</summary>
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0));

    /// <summary>SemVer rule: dot-separated identifiers, numeric ones compared as numbers.</summary>
    private static int ComparePreRelease(string left, string right)
    {
        var a = left.Split('.');
        var b = right.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var aNumeric = int.TryParse(a[i], out var an);
            var bNumeric = int.TryParse(b[i], out var bn);
            var result = (aNumeric, bNumeric) switch
            {
                (true, true) => an.CompareTo(bn),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(a[i], b[i]),
            };
            if (result != 0) return result;
        }
        return a.Length.CompareTo(b.Length);
    }
}
