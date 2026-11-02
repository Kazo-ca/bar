using DevToolbar.Core.Models;

namespace DevToolbar.Core.Layout;

/// <summary>
/// Host-neutral bookkeeping for <see cref="Interfaces.IHostSurfaceService"/>:
/// keeps one request per component and computes the surface to apply,
/// i.e. the base size grown to the largest request, clamped to what is available.
/// The base size is a floor: it is what the host always had before this protocol.
/// </summary>
public sealed class SurfaceSizeNegotiator
{
    private readonly Dictionary<string, SurfaceSize> _requests = new(StringComparer.Ordinal);

    public SurfaceSizeNegotiator(SurfaceSize baseSize)
    {
        if (!IsValid(baseSize) || double.IsInfinity(baseSize.Width) || double.IsInfinity(baseSize.Height))
            throw new ArgumentOutOfRangeException(nameof(baseSize), baseSize, "Base size must be finite and non-negative.");
        BaseSize = baseSize;
    }

    /// <summary>Surface used when no component asks for more (toolbar strip + room for menus).</summary>
    public SurfaceSize BaseSize { get; }

    /// <summary>Upper bound given by the host (work area from the window's position).</summary>
    public SurfaceSize Available { get; private set; } = SurfaceSize.Unbounded;

    public IReadOnlyDictionary<string, SurfaceSize> Requests => _requests;

    /// <summary>Records a request. Returns false (and ignores it) for an empty id or an invalid size.</summary>
    public bool Request(string componentId, SurfaceSize desired)
    {
        if (string.IsNullOrWhiteSpace(componentId) || !IsValid(desired)) return false;
        _requests[componentId] = desired;
        return true;
    }

    public bool Release(string componentId) =>
        !string.IsNullOrEmpty(componentId) && _requests.Remove(componentId);

    public void SetAvailable(SurfaceSize available)
    {
        if (!IsValid(available)) throw new ArgumentOutOfRangeException(nameof(available), available, "Available size must be non-negative.");
        Available = available;
    }

    /// <summary>Surface the host should apply now.</summary>
    public SurfaceSize Target
    {
        get
        {
            double width = 0, height = 0;
            foreach (var r in _requests.Values)
            {
                width = Math.Max(width, r.Width);
                height = Math.Max(height, r.Height);
            }
            width = Math.Max(BaseSize.Width, Math.Min(width, Available.Width));
            height = Math.Max(BaseSize.Height, Math.Min(height, Available.Height));
            return new SurfaceSize(Math.Ceiling(width), Math.Ceiling(height));
        }
    }

    private static bool IsValid(SurfaceSize s) =>
        !double.IsNaN(s.Width) && !double.IsNaN(s.Height) && s.Width >= 0 && s.Height >= 0;
}
