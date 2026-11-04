namespace Kazo.Bar.Web.Mocks;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

/// <summary>
/// Mock CI/CD service with sample GitHub Actions data and background polling support (US7.1).
/// </summary>
public class MockCiCdService : ICiCdService
{
    private readonly List<CiCdSession> _sessions;

    public event Action? OnSessionsUpdated;

    public TimeSpan PollingInterval => TimeSpan.FromSeconds(30);

    public MockCiCdService()
    {
        _sessions = new List<CiCdSession>
        {
            new CiCdSession
            {
                Id = "run-101",
                Name = "Build & Test",
                Status = CiCdStatus.Completed,
                Conclusion = "success",
                StartedAt = DateTime.Now.AddHours(-2),
                CompletedAt = DateTime.Now.AddHours(-1).AddMinutes(-45),
                Url = "https://github.com/example/my-webapi/actions/runs/101",
                Branch = "main",
                IsRead = false
            },
            new CiCdSession
            {
                Id = "run-102",
                Name = "Deploy to Staging",
                Status = CiCdStatus.Completed,
                Conclusion = "failure",
                StartedAt = DateTime.Now.AddHours(-1),
                CompletedAt = DateTime.Now.AddMinutes(-30),
                Url = "https://github.com/example/my-webapi/actions/runs/102",
                Branch = "feature/login",
                IsRead = false
            },
            new CiCdSession
            {
                Id = "run-103",
                Name = "Code Analysis",
                Status = CiCdStatus.InProgress,
                StartedAt = DateTime.Now.AddMinutes(-5),
                Url = "https://github.com/example/my-webapi/actions/runs/103",
                Branch = "main",
                IsRead = false
            }
        };
    }

    public async Task StartPollingAsync(string projectId, CancellationToken cancellationToken = default)
    {
        // Simulated background polling (US7.1)
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(PollingInterval, cancellationToken);

                // In a real implementation, this would call the GitHub API
                // For mock, we simulate an in-progress session completing
                var inProgress = _sessions.FirstOrDefault(s => s.Status == CiCdStatus.InProgress);
                if (inProgress != null && DateTime.Now - inProgress.StartedAt > TimeSpan.FromMinutes(1))
                {
                    inProgress.Status = CiCdStatus.Completed;
                    inProgress.Conclusion = "success";
                    inProgress.CompletedAt = DateTime.Now;
                    OnSessionsUpdated?.Invoke();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Polling cancelled - expected when project changes or app shuts down
        }
    }

    public Task<IReadOnlyList<CiCdSession>> GetSessionsAsync(string projectId) =>
        Task.FromResult<IReadOnlyList<CiCdSession>>(_sessions.AsReadOnly());

    public int GetUnreadCount(string projectId) =>
        _sessions.Count(s => !s.IsRead && s.Status == CiCdStatus.Completed);

    public void MarkAsRead(string sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null)
        {
            session.IsRead = true;
            OnSessionsUpdated?.Invoke();
        }
    }

    public void MarkAllAsRead(string projectId)
    {
        foreach (var session in _sessions)
        {
            session.IsRead = true;
        }
        OnSessionsUpdated?.Invoke();
    }
}
