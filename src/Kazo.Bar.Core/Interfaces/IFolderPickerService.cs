namespace Kazo.Bar.Core.Interfaces;

/// <summary>Native "choose a folder" dialog, used to link a repository to its local folder.</summary>
public interface IFolderPickerService
{
    /// <summary>Shows the dialog; returns the chosen folder, or null when cancelled.</summary>
    Task<string?> PickFolderAsync(string? initialFolder = null);
}
