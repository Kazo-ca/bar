using System.Windows;
using Kazo.Bar.Core.Interfaces;
using Microsoft.Win32;

namespace Kazo.Bar.Windows.Services;

/// <summary>Native Windows folder dialog (WPF <see cref="OpenFolderDialog"/>), shown over the toolbar.</summary>
public sealed class WpfFolderPickerService : IFolderPickerService
{
    public Task<string?> PickFolderAsync(string? initialFolder = null)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("The WPF application is not running.");
        return dispatcher.InvokeAsync(() =>
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Choose the repository's local folder",
                Multiselect = false,
            };
            if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
                dialog.InitialDirectory = initialFolder;
            var owner = Application.Current.MainWindow;
            var accepted = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
            return accepted == true ? dialog.FolderName : null;
        }).Task;
    }
}
