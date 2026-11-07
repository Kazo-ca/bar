using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Kazo.Bar.Updates;

namespace Kazo.Bar.Updater;

/// <summary>Small progress window: headline, detail, progress bar, Cancel / Close.</summary>
internal sealed class UpdaterWindow : Window
{
    private static readonly TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(4);

    private readonly UpdaterArguments _args;
    private readonly TextBlock _headline = new() { FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _detail = new() { Margin = new Thickness(0, 6, 0, 12), TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { Height = 8, Minimum = 0, Maximum = 1, IsIndeterminate = true };
    private readonly Button _button = new() { Content = "Cancel", MinWidth = 90, Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
    private readonly CancellationTokenSource _cancel = new();
    private UpdateWorkflow? _workflow;
    private bool _finished;

    public int ExitCode { get; private set; }

    public UpdaterWindow(UpdaterArguments args)
    {
        _args = args;
        Title = "Kazo Bar Updater";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(_headline);
        panel.Children.Add(_detail);
        panel.Children.Add(_progress);
        panel.Children.Add(_button);
        Content = panel;

        _button.Click += (_, _) => OnButton();
        Closing += (_, e) =>
        {
            if (_finished) return;
            if (_workflow?.IsPastPointOfNoReturn == true) { e.Cancel = true; return; }
            _cancel.Cancel();
        };
        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _workflow = new UpdateWorkflow(_args, http, new Progress<UpdateStatus>(Show));
        try
        {
            await _workflow.RunAsync(_cancel.Token);
            ExitCode = 0;
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
            UpdaterLog.Write("Cancelled by the user.");
            Show(new UpdateStatus("Update cancelled", "Kazo Bar was not changed.", 0, Finished: true));
            ExitCode = 2;
        }
        catch (Exception ex)
        {
            UpdaterLog.Write($"Update failed: {ex}");
            Show(new UpdateStatus("Update failed", Describe(ex), 0, Finished: true, Failed: true));
            ExitCode = 1;
        }

        if (ExitCode == 0)
        {
            var timer = new DispatcherTimer { Interval = AutoCloseDelay };
            timer.Tick += (_, _) => { timer.Stop(); Close(); };
            timer.Start();
        }
    }

    private void Show(UpdateStatus status)
    {
        _headline.Text = status.Headline;
        _detail.Text = status.Detail;
        _progress.IsIndeterminate = status.Fraction == null;
        _progress.Value = status.Fraction ?? 0;
        _progress.Visibility = status.Failed ? Visibility.Collapsed : Visibility.Visible;
        _button.IsEnabled = status.Finished || _workflow?.IsPastPointOfNoReturn != true;
        if (status.Finished)
        {
            _finished = true;
            _button.Content = "Close";
        }
    }

    private void OnButton()
    {
        if (_finished) Close();
        else _cancel.Cancel();
    }

    private static string Describe(Exception ex) => ex switch
    {
        HttpRequestException => $"GitHub could not be reached. Check the internet connection and try again.\n\n{ex.Message}\n\nLog: {UpdaterLog.Path}",
        TaskCanceledException => $"GitHub did not answer in time. Try again later.\n\nLog: {UpdaterLog.Path}",
        _ => $"{ex.Message}\n\nLog: {UpdaterLog.Path}",
    };
}
