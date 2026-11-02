using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DevToolbar.Feasibility;

/// <summary>A native input witness. Run in its own process, behind the web prototype.</summary>
public sealed class WitnessWindow : Window
{
    private readonly Action<string, object?> _log;
    private readonly TextBox _text;
    private readonly TextBlock _status;
    private int _inputEvents;
    private int _edits;
    private int _selections;

    public WitnessWindow(Action<string, object?> log)
    {
        _log = log;
        Title = "DevToolbar feasibility — witness";
        Left = 40;
        Top = 80;
        Width = 1100;
        Height = 700;
        Background = Brushes.White;

        _status = new TextBlock
        {
            Margin = new Thickness(12),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Black,
            FontSize = 14,
        };
        _text = new TextBox
        {
            Text = string.Join(Environment.NewLine, Enumerable.Range(1, 100).Select(i =>
                $"{i:000} — Alpha Bravo Charlie Delta. Double-cliquez un mot ; clic droit pour le menu ; molette pour défiler.")),
            AcceptsReturn = true,
            AcceptsTab = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 16,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            Padding = new Thickness(8),
        };
        // Leave ContextMenu unset: TextBox supplies its standard editing menu.
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.Children.Add(_status);
        Grid.SetRow(_text, 1);
        grid.Children.Add(_text);
        Content = grid;
        UpdateStatus("Prêt : placez le prototype au-dessus de ce témoin.");

        PreviewMouseDown += (_, e) => RecordMouse("mouse-down", e);
        PreviewMouseUp += (_, e) => RecordMouse("mouse-up", e);
        PreviewMouseWheel += (_, e) =>
        {
            _inputEvents++;
            var p = e.GetPosition(this);
            var screen = PointToScreen(p);
            _log("mouse-wheel", new { delta = e.Delta, x = p.X, y = p.Y,
                screenX = screen.X, screenY = screen.Y, timestamp = e.Timestamp });
            UpdateStatus($"Molette : {e.Delta}");
        };
        PreviewKeyDown += (_, e) => RecordKey("key-down", e);
        PreviewKeyUp += (_, e) => RecordKey("key-up", e);
        Activated += (_, _) =>
        {
            _log("activated", new { isActive = IsActive });
            UpdateStatus("Témoin activé");
        };
        Deactivated += (_, _) =>
        {
            _log("deactivated", new { isActive = IsActive });
            UpdateStatus("Témoin désactivé");
        };
        _text.TextChanged += (_, _) =>
        {
            _edits++;
            _log("text-changed", new { edits = _edits, characters = _text.Text.Length,
                lines = _text.LineCount, selectionStart = _text.SelectionStart,
                selectionLength = _text.SelectionLength });
            UpdateStatus("Texte modifié");
        };
        _text.SelectionChanged += (_, _) =>
        {
            _selections++;
            _log("selection-changed", new { changes = _selections,
                selectionStart = _text.SelectionStart, selectionLength = _text.SelectionLength });
        };
        _text.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, e) =>
        {
            if (e.VerticalChange != 0 || e.HorizontalChange != 0)
                _log("scroll-changed", new { verticalOffset = e.VerticalOffset,
                    horizontalOffset = e.HorizontalOffset, verticalChange = e.VerticalChange,
                    horizontalChange = e.HorizontalChange });
        }));
        Loaded += (_, _) =>
        {
            _log("loaded", new { left = Left, top = Top, width = ActualWidth,
                height = ActualHeight, dpi = VisualTreeHelper.GetDpi(this).PixelsPerInchX });
            _text.Focus();
        };
        Closed += (_, _) => _log("closed", null);
    }

    private void RecordMouse(string kind, MouseButtonEventArgs e)
    {
        _inputEvents++;
        var p = e.GetPosition(this);
        var screen = PointToScreen(p);
        _log(kind, new { button = e.ChangedButton.ToString(), clickCount = e.ClickCount,
            x = p.X, y = p.Y, screenX = screen.X, screenY = screen.Y,
            timestamp = e.Timestamp });
        UpdateStatus($"{kind} : {e.ChangedButton}, nombre de clics {e.ClickCount}");
    }

    private void RecordKey(string kind, KeyEventArgs e)
    {
        _inputEvents++;
        _log(kind, new { key = e.Key.ToString(), systemKey = e.SystemKey.ToString(),
            modifiers = Keyboard.Modifiers.ToString(), isRepeat = e.IsRepeat,
            timestamp = e.Timestamp });
        UpdateStatus($"{kind} : {e.Key}");
    }

    private void UpdateStatus(string latest) => _status.Text =
        $"Témoin natif indépendant — entrées : {_inputEvents} ; modifications : {_edits}. {latest}\n" +
        "Utilisez clic, double-clic, clic droit et molette sous les zones transparentes. " +
        "Le journal constate seulement les événements réellement reçus par ce processus.";
}
