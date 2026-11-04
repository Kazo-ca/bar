using Kazo.Bar.Maui.Themes;

namespace Kazo.Bar.Maui.ViewModels;

/// <summary>
/// ViewModel for the Settings window.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ToolbarViewModel _toolbarVm;

    public SettingsViewModel(ToolbarViewModel toolbarVm)
    {
        _toolbarVm = toolbarVm;
        _selectedTheme = toolbarVm.CurrentTheme;

        SetThemeCommand = new RelayCommand<object?>(param =>
        {
            if (param is string theme)
            {
                SelectedTheme = theme;
                _toolbarVm.CurrentTheme = theme;
                OnPropertyChanged(nameof(IsLightTheme));
                OnPropertyChanged(nameof(IsDarkTheme));
                OnPropertyChanged(nameof(IsGreenTheme));
            }
        });

        CloseCommand = new RelayCommand(() =>
        {
            // Close the last opened window (the settings window)
            var windows = Application.Current?.Windows;
            if (windows != null && windows.Count > 1)
            {
                var settingsWindow = windows[^1];
                Application.Current?.CloseWindow(settingsWindow);
            }
        });
    }

    private string _selectedTheme;
    public string SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    public bool IsLightTheme => SelectedTheme == "Light";
    public bool IsDarkTheme => SelectedTheme == "Dark";
    public bool IsGreenTheme => SelectedTheme == "Green";

    public RelayCommand<object?> SetThemeCommand { get; }
    public RelayCommand CloseCommand { get; }
}
