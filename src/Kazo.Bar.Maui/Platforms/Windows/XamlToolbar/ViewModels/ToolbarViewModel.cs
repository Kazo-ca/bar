using System.Collections.ObjectModel;
using Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.Themes;

namespace Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.ViewModels;

/// <summary>
/// Represents a project in the dropdown list.
/// </summary>
public class ProjectItem : ViewModelBase
{
    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _iconGlyph = "\uE8A5"; // default: repo icon
    public string IconGlyph { get => _iconGlyph; set => SetProperty(ref _iconGlyph, value); }
}

/// <summary>
/// Main ViewModel for the XAML toolbar. Drives all UI bindings.
/// </summary>
public class ToolbarViewModel : ViewModelBase
{
    // ── Theme ────────────────────────────────────────────────────────
    private ToolbarTheme _currentTheme = ToolbarTheme.Light;
    public ToolbarTheme CurrentTheme
    {
        get => _currentTheme;
        set
        {
            if (SetProperty(ref _currentTheme, value))
            {
                Theme = ThemeManager.GetTheme(value);
                OnPropertyChanged(nameof(Theme));
            }
        }
    }

    private ThemeColors _theme = ThemeManager.GetTheme(ToolbarTheme.Light);
    public ThemeColors Theme { get => _theme; private set => SetProperty(ref _theme, value); }

    // ── Project ──────────────────────────────────────────────────────
    private string _projectName = "No Project";
    public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value); }

    private bool _isDropdownOpen;
    public bool IsDropdownOpen { get => _isDropdownOpen; set => SetProperty(ref _isDropdownOpen, value); }

    public ObservableCollection<ProjectItem> Projects { get; } = new()
    {
        new ProjectItem { Name = "Projet Alpha" },
        new ProjectItem { Name = "Projet Beta" },
        new ProjectItem { Name = "Projet Gamma" },
    };

    // ── Git ──────────────────────────────────────────────────────────
    private string _branchName = "main";
    public string BranchName { get => _branchName; set => SetProperty(ref _branchName, value); }

    private bool _hasPendingSync;
    public bool HasPendingSync { get => _hasPendingSync; set => SetProperty(ref _hasPendingSync, value); }

    // ── Timer ────────────────────────────────────────────────────────
    private string _timerText = "00:00";
    public string TimerText { get => _timerText; set => SetProperty(ref _timerText, value); }

    private bool _isTimerRunning;
    public bool IsTimerRunning { get => _isTimerRunning; set => SetProperty(ref _isTimerRunning, value); }

    // ── Commands ─────────────────────────────────────────────────────
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
            // TODO: Wire to real timer service
        });

        OpenSettingsCommand = new RelayCommand(() =>
        {
            // TODO: Open settings panel
        });

        RunActionCommand = new RelayCommand(() =>
        {
            // TODO: Run/build action
        });

        OpenBrowserCommand = new RelayCommand(() =>
        {
            // TODO: Open browser
        });

        OpenFolderCommand = new RelayCommand(() =>
        {
            // TODO: Open folder
        });

        CycleThemeCommand = new RelayCommand(() =>
        {
            CurrentTheme = CurrentTheme switch
            {
                ToolbarTheme.Light => ToolbarTheme.Dark,
                ToolbarTheme.Dark => ToolbarTheme.Green,
                ToolbarTheme.Green => ToolbarTheme.Light,
                _ => ToolbarTheme.Light,
            };
        });

        // Start with "Projet Alpha" selected
        ProjectName = "Projet Alpha";
    }

    /// <summary>
    /// Start the timer display update loop.
    /// </summary>
    public async Task StartTimerLoopAsync(CancellationToken ct)
    {
        var startTime = DateTime.Now;
        while (!ct.IsCancellationRequested)
        {
            if (IsTimerRunning)
            {
                var elapsed = DateTime.Now - startTime;
                TimerText = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
            }
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Generic version of RelayCommand that carries a typed parameter.
/// </summary>
public class RelayCommand<T> : System.Windows.Input.ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
