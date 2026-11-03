namespace DevToolbar.Infrastructure.Providers;

using System.Globalization;
using System.Text.Json.Serialization;
using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Real <see cref="ICiCdService"/> (US7.1, US7.2): recent GitHub Actions workflow runs of every
/// repository of the project that has <see cref="GitHubTracking.TrackActions"/>, polled in the
/// background. "Read" states are saved per project with <see cref="IStorageService"/>. The first
/// time a project is read, the runs already finished count as read, so only new results show up.
/// </summary>
public sealed class GitHubActionsService : ICiCdService
{
    public const string StorageNamespace = "github-actions";
    private const int RunsPerRepository = 10;
    private const int MaxKeptReadIds = 500;
    private const int MinimumPollingSeconds = 30;

    private readonly GitHubApi _api;
    private readonly ISettingsService _settings;
    private readonly IGlobalSettingsService _globalSettings;
    private readonly IStorageService _storage;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, IReadOnlyList<CiCdSession>> _sessions = new();
    private readonly Dictionary<string, HashSet<string>> _read = new();

    public GitHubActionsService(GitHubApi api, ISettingsService settings, IGlobalSettingsService globalSettings,
        IStorageService storage, ILogger<GitHubActionsService>? logger = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _globalSettings = globalSettings ?? throw new ArgumentNullException(nameof(globalSettings));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger ?? NullLogger<GitHubActionsService>.Instance;
    }

    public event Action? OnSessionsUpdated;

    /// <summary>Last error while polling, for display; null when the last poll worked.</summary>
    public string? LastError { get; private set; }

    public TimeSpan PollingInterval =>
        TimeSpan.FromSeconds(Math.Max(MinimumPollingSeconds, _globalSettings.Current.CiPollingIntervalSeconds));

    public async Task<IReadOnlyList<CiCdSession>> GetSessionsAsync(string projectId)
    {
        await RefreshAsync(projectId, CancellationToken.None);
        lock (_gate)
            return _sessions.TryGetValue(projectId, out var sessions) ? sessions : [];
    }

    public int GetUnreadCount(string projectId)
    {
        lock (_gate)
            return _sessions.TryGetValue(projectId, out var sessions) ? sessions.Count(s => !s.IsRead) : 0;
    }

    public void MarkAsRead(string sessionId) => MarkRead(s => s.Id == sessionId);

    public void MarkAllAsRead(string projectId) => MarkRead(_ => true, projectId);

    public async Task StartPollingAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var timer = new PeriodicTimer(PollingInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (await RefreshAsync(projectId, cancellationToken))
                    OnSessionsUpdated?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            // Project changed or app closing.
        }
    }

    /// <summary>Reads the runs of the project; returns true when the list or a state changed.</summary>
    public async Task<bool> RefreshAsync(string projectId, CancellationToken cancellationToken)
    {
        var project = (await _settings.GetProjectsAsync()).FirstOrDefault(p => p.Id == projectId);
        var tracked = project?.GitRepositories
            .Select(r => r.GitHub)
            .Where(g => g is { TrackActions: true } && !string.IsNullOrWhiteSpace(g.Repository))
            .Select(g => g!)
            .ToList() ?? [];

        var runs = new List<CiCdSession>();
        string? error = null;
        foreach (var tracking in tracked)
        {
            try
            {
                runs.AddRange(await ReadRunsAsync(tracking, cancellationToken));
            }
            catch (ProviderException ex)
            {
                error = $"{tracking.Repository}: {ex.Message}";
                _logger.LogWarning("GitHub Actions poll failed for {Repository}: {Message}", tracking.Repository, ex.Message);
            }
        }
        LastError = error;

        var read = await LoadReadAsync(projectId, runs);
        var sessions = runs
            .OrderByDescending(r => r.StartedAt)
            .Select(r => { r.IsRead = r.Status != CiCdStatus.Completed || read.Contains(r.Id); return r; })
            .ToList();

        lock (_gate)
        {
            var changed = !_sessions.TryGetValue(projectId, out var previous) || !Same(previous, sessions);
            _sessions[projectId] = sessions;
            return changed;
        }
    }

    private async Task<IEnumerable<CiCdSession>> ReadRunsAsync(GitHubTracking tracking, CancellationToken cancellationToken)
    {
        var url = $"{GitHubApi.RepositoryPath(tracking.Repository)}/actions/runs?per_page={RunsPerRepository}";
        var result = await _api.GetAsync<RunList>(url, tracking.SecretRef, cancellationToken);
        return result.WorkflowRuns.Select(run => ToSession(tracking.Repository, run));
    }

    internal static CiCdSession ToSession(string repository, Run run)
    {
        var status = run.Status switch
        {
            "completed" => CiCdStatus.Completed,
            "in_progress" => CiCdStatus.InProgress,
            _ => CiCdStatus.Queued,
        };
        return new CiCdSession
        {
            Id = $"{repository}#{run.Id.ToString(CultureInfo.InvariantCulture)}",
            Name = $"{run.Name} #{run.RunNumber.ToString(CultureInfo.InvariantCulture)}",
            Status = status,
            Conclusion = run.Conclusion,
            StartedAt = (run.RunStartedAt ?? run.CreatedAt).ToLocalTime(),
            CompletedAt = status == CiCdStatus.Completed ? run.UpdatedAt.ToLocalTime() : null,
            Url = run.HtmlUrl,
            Branch = run.HeadBranch ?? string.Empty,
        };
    }

    private async Task<HashSet<string>> LoadReadAsync(string projectId, IReadOnlyList<CiCdSession> runs)
    {
        HashSet<string> read;
        lock (_gate)
            _read.TryGetValue(projectId, out read!);

        if (read == null)
        {
            var saved = await _storage.GetAsync<ReadState>(StorageNamespace, ReadKey(projectId));
            read = saved?.Ids.ToHashSet() ?? [];
            if (saved == null)
            {
                // Nothing read yet: wait for runs before taking the baseline.
                if (runs.Count == 0)
                    return read;
                // First look at this project: what already finished is not news.
                read.UnionWith(runs.Where(r => r.Status == CiCdStatus.Completed).Select(r => r.Id));
                await SaveReadAsync(projectId, read);
            }
            lock (_gate)
                _read[projectId] = read;
        }
        return read;
    }

    private void MarkRead(Func<CiCdSession, bool> match, string? onlyProjectId = null)
    {
        var toSave = new List<(string ProjectId, HashSet<string> Ids)>();
        lock (_gate)
        {
            foreach (var (projectId, sessions) in _sessions)
            {
                if (onlyProjectId != null && projectId != onlyProjectId)
                    continue;
                var hits = sessions.Where(s => !s.IsRead && match(s)).ToList();
                if (hits.Count == 0)
                    continue;
                if (!_read.TryGetValue(projectId, out var ids))
                    _read[projectId] = ids = [];
                foreach (var session in hits)
                {
                    session.IsRead = true;
                    ids.Add(session.Id);
                }
                toSave.Add((projectId, ids.ToHashSet()));
            }
        }
        foreach (var (projectId, ids) in toSave)
            _ = SaveReadInBackgroundAsync(projectId, ids);
    }

    private async Task SaveReadInBackgroundAsync(string projectId, HashSet<string> ids)
    {
        try
        {
            await SaveReadAsync(projectId, ids);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogError(ex, "Could not save GitHub Actions read state for {ProjectId}", projectId);
        }
    }

    private Task SaveReadAsync(string projectId, HashSet<string> ids) =>
        _storage.SetAsync(StorageNamespace, ReadKey(projectId), new ReadState(ids.TakeLast(MaxKeptReadIds).ToList()));

    private static string ReadKey(string projectId) => $"read:{projectId}";

    private static bool Same(IReadOnlyList<CiCdSession> a, IReadOnlyList<CiCdSession> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Id == p.Second.Id && p.First.Status == p.Second.Status
            && p.First.Conclusion == p.Second.Conclusion && p.First.IsRead == p.Second.IsRead);

    internal sealed record ReadState(List<string> Ids);

    internal sealed record RunList([property: JsonPropertyName("workflow_runs")] List<Run> WorkflowRuns);

    internal sealed record Run(
        long Id,
        string Name,
        [property: JsonPropertyName("run_number")] int RunNumber,
        string Status,
        string? Conclusion,
        [property: JsonPropertyName("head_branch")] string? HeadBranch,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("updated_at")] DateTime UpdatedAt,
        [property: JsonPropertyName("run_started_at")] DateTime? RunStartedAt);
}
