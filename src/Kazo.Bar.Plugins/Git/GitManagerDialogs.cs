namespace Kazo.Bar.Plugins.Git;

using Kazo.Bar.Core.Dialogs;

/// <summary>Opens the Git repository window (US5.7) through the dialog service.</summary>
public static class GitManagerDialogs
{
    /// <summary>Single instance: opening it again brings the window to the front on the asked tab.</summary>
    public const string Key = "git-manager";

    public static async Task OpenGitManagerAsync(this IDialogService dialogs, GitPlugin plugin, GitManagerTab tab)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(plugin);
        if (plugin.Manager == null || plugin.SelectedRepository == null) return;
        // An open window follows this request; a new one starts on InitialTab.
        plugin.RequestManagerTab(tab);
        await dialogs.OpenAsync<GitManagerDialog>(
            new Dictionary<string, object?>
            {
                [nameof(GitManagerDialog.Plugin)] = plugin,
                [nameof(GitManagerDialog.InitialTab)] = tab,
            },
            new DialogOptions
            {
                Title = $"Kazo Bar — Git — {plugin.SelectedRepository.Name}",
                Width = 1100,
                Height = 740,
                Key = Key,
                InPageFrame = false,
            });
    }
}
