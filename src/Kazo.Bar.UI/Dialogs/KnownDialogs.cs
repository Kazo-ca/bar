using Kazo.Bar.Core.Dialogs;
using Kazo.Bar.UI.Components;
using Kazo.Bar.UI.Pages;

namespace Kazo.Bar.UI.Dialogs;

/// <summary>The application's own dialogs, opened from the toolbar and the tray menu.</summary>
public static class KnownDialogs
{
    public static readonly DialogOptions SettingsOptions = new()
    {
        Title = "Kazo Bar — Settings",
        Width = 880,
        Height = 860,
        FallbackRoute = "/settings",
    };

    public static readonly DialogOptions TimeReportOptions = new()
    {
        Title = "Kazo Bar — Time Report",
        Width = 720,
        Height = 720,
        InPageFrame = false,
    };

    /// <summary>Project settings (name, folder, theme...) of <paramref name="projectId"/>: one window per project.</summary>
    public static Task<IDialogReference> OpenProjectSettingsAsync(this IDialogService dialogs, string projectId) =>
        dialogs.OpenAsync<ProjectSettings>(
            new Dictionary<string, object?> { [nameof(ProjectSettings.ProjectId)] = projectId },
            new DialogOptions
            {
                Title = "Kazo Bar — Project settings",
                Width = 820,
                Height = 820,
                Key = $"project-settings:{projectId}",
                FallbackRoute = $"/project-settings/{Uri.EscapeDataString(projectId)}",
            });

    public static Task<IDialogReference> OpenSettingsAsync(this IDialogService dialogs) =>
        dialogs.OpenAsync<Settings>(options: SettingsOptions);

    public static Task<IDialogReference> OpenTimeReportAsync(this IDialogService dialogs) =>
        dialogs.OpenAsync<TimeReport>(new Dictionary<string, object?> { [nameof(TimeReport.IsVisible)] = true }, TimeReportOptions);
}
