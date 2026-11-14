namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>Why a whole day (or part of it) was not worked (US5.9).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DayOffKind>))]
public enum DayOffKind
{
    Sick,
    Vacation,
    /// <summary>Public holiday.</summary>
    Holiday,
    /// <summary>Personal leave (appointment, family...).</summary>
    Personal,
    /// <summary>Something else, described by <see cref="DayOff.Label"/>.</summary>
    Other,
}

/// <summary>
/// An absence label put on a day after the fact, usually the next day (US5.9): sick, vacation...
/// Never counted as worked time.
/// </summary>
public sealed class DayOff
{
    public const double MaxHours = 24;

    [JsonPropertyName("kind")]
    public DayOffKind Kind { get; set; }

    /// <summary>Free text ("Dentist", "Flu"...).</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>Hours off; null for the whole day.</summary>
    [JsonPropertyName("hours")]
    public double? Hours { get; set; }

    public DayOff Copy() => new() { Kind = Kind, Label = Label, Hours = Hours };

    /// <summary>
    /// A clean copy to store: label trimmed (null when blank), hours between 0 and 24 excluded/included.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for hours out of range or an unknown kind.
    /// </summary>
    public DayOff Normalized()
    {
        if (!Enum.IsDefined(Kind))
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown absence kind.");
        if (Hours is { } hours && (double.IsNaN(hours) || hours <= 0 || hours > MaxHours))
            throw new ArgumentOutOfRangeException(nameof(Hours), hours, "Hours off must be more than 0 and at most 24.");
        return new DayOff
        {
            Kind = Kind,
            Label = string.IsNullOrWhiteSpace(Label) ? null : Label.Trim(),
            Hours = Hours,
        };
    }
}
