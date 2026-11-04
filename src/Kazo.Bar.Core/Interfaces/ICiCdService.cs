namespace Kazo.Bar.Core.Interfaces;

using Kazo.Bar.Core.Models;

/// <summary>
/// Abstracts CI/CD status polling (GitHub Actions, Azure Pipelines, etc.)
/// </summary>
public interface ICiCdService
{
    /// <summary>Get recent sessions for a project.</summary>
    Task<IReadOnlyList<CiCdSession>> GetSessionsAsync(string projectId);

    /// <summary>Get count of unread completed sessions.</summary>
    int GetUnreadCount(string projectId);

    /// <summary>Mark a session as read.</summary>
    void MarkAsRead(string sessionId);

    /// <summary>Mark all sessions as read for a project.</summary>
    void MarkAllAsRead(string projectId);

    /// <summary>Event fired when sessions are updated.</summary>
    event Action? OnSessionsUpdated;

    /// <summary>Polling interval for background refresh (US7.1).</summary>
    TimeSpan PollingInterval { get; }

    /// <summary>Start background polling for CI/CD session updates.</summary>
    Task StartPollingAsync(string projectId, CancellationToken cancellationToken = default);
}
