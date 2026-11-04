using Kazo.Bar.Core.Models;

namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Two-way size negotiation between UI components and the host window.
///
/// Page → host: a component declares the surface it needs to be shown without
/// clipping (<see cref="RequestSize"/>), and withdraws the request when it goes
/// away (<see cref="ReleaseSize"/>). The host grows its window to the largest
/// request, never past <see cref="Available"/>.
///
/// Host → page: <see cref="Current"/> and <see cref="Available"/> report what the
/// host actually gave and what it could give; <see cref="Changed"/> fires when
/// either moves (resize, dock change, other monitor). Whatever does not fit
/// scrolls inside the component.
///
/// Optional: hosts whose surface cannot change (browser, MAUI page) do not
/// register it, and components then rely on CSS scrolling only.
/// </summary>
public interface IHostSurfaceService
{
    /// <summary>Size of the host surface right now (CSS px).</summary>
    SurfaceSize Current { get; }

    /// <summary>Largest surface the host can give from its current position (CSS px).</summary>
    SurfaceSize Available { get; }

    /// <summary>Raised after <see cref="Current"/> or <see cref="Available"/> changed.</summary>
    event Action? Changed;

    /// <summary>
    /// Declares (or updates) the surface size needed by <paramref name="componentId"/>,
    /// measured from the window's anchored edge, so the whole component is visible.
    /// </summary>
    void RequestSize(string componentId, SurfaceSize desired);

    /// <summary>Withdraws the request of <paramref name="componentId"/> (no-op if none).</summary>
    void ReleaseSize(string componentId);
}
