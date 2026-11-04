namespace Kazo.Bar.Maui.Views;

/// <summary>
/// Settings page for the Kazo Bar application.
/// Opens as a separate window. Allows theme selection and general settings.
/// </summary>
public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        // Register converter before InitializeComponent
        Resources.Add("BoolToStrokeConverter", new BoolToStrokeConverter());
        InitializeComponent();
    }
}

/// <summary>
/// Converts a boolean (is-selected) to a stroke color for theme cards.
/// Selected = accent blue, unselected = transparent.
/// </summary>
public class BoolToStrokeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        bool isSelected = value is true;
        return isSelected
            ? new SolidColorBrush(Color.FromArgb("#5EA0F4"))
            : new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
