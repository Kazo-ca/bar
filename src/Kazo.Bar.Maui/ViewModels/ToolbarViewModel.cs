using System.Collections.ObjectModel;
using Kazo.Bar.Maui.Themes;

namespace Kazo.Bar.Maui.ViewModels;

/// <summary>
/// Represents a project in the dropdown list.
/// </summary>
public class ProjectItem : ViewModelBase
{
    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _iconGlyph = "\uE895";
    public string IconGlyph { get => _iconGlyph; set => SetProperty(ref _iconGlyph, value); }
}

/// <summary>
/// Main ViewModel driving the native XAML toolbar. All UI bindings flow through this ViewModel.
/// </summary>
public class ToolbarViewModel : ViewModelBase
{
    // ── Theme ────────────────────────────────────────────────────

    private string _currentTheme = "Light";
    public string CurrentTheme
    {
        get => _currentTheme;
        set
        {
            if (SetProperty(ref _currentTheme, value))
                ThemeResourceManager.ApplyTheme(value);
        }
    }

    // ── Project ──────────────────────────────────────────────────

    private string _projectName = "Projet Alpha";
    public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value); }

    private bool _isDropdownOpen;
    public bool IsDropdownOpen { get => _isDropdownOpen; set => SetProperty(ref _isDropdownOpen, value); }

    public ObservableCollection<ProjectItem> Projects { get; } = new()
    {
        new ProjectItem { Name = "Projet Alpha" },
        new ProjectItem { Name = "Projet Beta" },
        new ProjectItem { Name = "Projet Gamma" },
    };

    // ── Git ──────────────────────────────────────────────────────

    private string _branchName = "main";
    public string BranchName { get => _branchName; set => SetProperty(ref _branchName, value); }

    private bool _hasPendingSync;
    public bool HasPendingSync { get => _hasPendingSync; set => SetProperty(ref _hasPendingSync, value); }

    // ── Timer ────────────────────────────────────────────────────

    private string _timerText = "01:23";
    public string TimerText { get => _timerText; set => SetProperty(ref _timerText, value); }

    private bool _isTimerRunning;
    public bool IsTimerRunning { get => _isTimerRunning; set => SetProperty(ref _isTimerRunning, value); }

    // ── Commands ─────────────────────────────────────────────────

    public RelayCommand ToggleDropdownCommand { get; }
    public RelayCommand<object?> SelectProjectCommand { get; }
    public RelayCommand ToggleTimerCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand RunActionCommand { get; }
    public RelayCommand OpenBrowserCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand CycleThemeCommand { get; }

    public ToolbarViewModel()
    {
        ToggleDropdownCommand = new RelayCommand(() => IsDropdownOpen = !IsDropdownOpen);

        SelectProjectCommand = new RelayCommand<object?>(param =>
        {
            if (param is ProjectItem project)
            {
                ProjectName = project.Name;
                IsDropdownOpen = false;
            }
        });

        ToggleTimerCommand = new RelayCommand(() =>
        {
            IsTimerRunning = !IsTimerRunning;
        });

        OpenSettingsCommand = new RelayCommand(OpenSettings);

        RunActionCommand = new RelayCommand(() =>
        {
            // TODO: Wire to real run/build action
        });

        OpenBrowserCommand = new RelayCommand(() =>
        {
            // TODO: Wire to real browser open
        });

        OpenFolderCommand = new RelayCommand(() =>
        {
            // TODO: Wire to real folder open
        });

        CycleThemeCommand = new RelayCommand(() =>
        {
            CurrentTheme = CurrentTheme switch
            {
                "Light" => "Dark",
                "Dark" => "Green",
                _ => "Light",
            };
        });
    }

    private void OpenSettings()
    {
        var settingsPage = new Views.SettingsPage
        {
            BindingContext = new SettingsViewModel(this)
        };
        var settingsWindow = new Window(settingsPage)
        {
            Title = "Kazo Bar - Settings",
            Width = 450,
            Height = 600,
        };
        Application.Current?.OpenWindow(settingsWindow);
    }

    /// <summary>
    /// Start the timer display update loop (runs on background thread, dispatches to UI).
    /// </summary>
    public async Task StartTimerLoopAsync(CancellationToken ct)
    {
        var startTime = DateTime.Now;
        while (!ct.IsCancellationRequested)
        {
            if (IsTimerRunning)
            {
                var elapsed = DateTime.Now - startTime;
                MainThread.BeginInvokeOnMainThread(() =>
                    TimerText = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}");
            }
            try { await Task.Delay(1000, ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
