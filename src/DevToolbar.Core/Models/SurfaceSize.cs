namespace DevToolbar.Core.Models;

/// <summary>A size in CSS pixels (WPF device-independent pixels on the Windows host).</summary>
public readonly record struct SurfaceSize(double Width, double Height)
{
    public static readonly SurfaceSize Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);
}
