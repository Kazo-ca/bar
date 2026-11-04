namespace DevToolbar.Tests.E2E.Scenarios;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
/// Playwright tests that capture screenshots for project documentation.
/// Screenshots are saved to doc/screenshots/ in the repository root.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class ScreenshotTests : PageTest
{
    private const string BaseUrl = "http://localhost:5280";
    private System.Diagnostics.Process? _serverProcess;
    private string _screenshotsDir = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Resolve doc/screenshots directory relative to test project
        _screenshotsDir = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..",
                "doc", "screenshots"));
        Directory.CreateDirectory(_screenshotsDir);

        // Start the web server
        var projectPath = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..",
                "src", "DevToolbar.Web"));

        _serverProcess = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\" --urls {BaseUrl}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment = { ["ASPNETCORE_ENVIRONMENT"] = "Development" }
            }
        };

        _serverProcess.Start();

        // Wait for server to be ready
        using var httpClient = new HttpClient();
        var maxRetries = 60;
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await httpClient.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode)
                    break;
            }
            catch
            {
                // Server not ready yet
            }
            await Task.Delay(1000);
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill(entireProcessTree: true);
            _serverProcess.Dispose();
        }
    }

    private async Task NavigateAndWait()
    {
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".toolbar-shell")).ToBeVisibleAsync(new() { Timeout = 30000 });
        // Wait for interactive mode by checking that the git plugin panel is rendered
        await Expect(Page.Locator(".module-git .plugin-panel").First).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    /// <summary>
    /// Switch to a project by opening the dropdown and clicking the item.
    /// </summary>
    private async Task SwitchProject(string projectName)
    {
        await Page.Locator(".project-selector-trigger").ClickAsync();
        await Expect(Page.Locator(".project-dropdown")).ToBeVisibleAsync(new() { Timeout = 3000 });
        await Page.Locator(".project-dropdown-item", new() { HasText = projectName }).ClickAsync();
        await Expect(Page.Locator(".project-name")).ToContainTextAsync(projectName, new() { Timeout = 5000 });
    }

    private string ScreenshotPath(string name) => Path.Combine(_screenshotsDir, $"{name}.png");

    // ─── Screenshot Capture Tests ───

    [Test, Order(1)]
    public async Task Capture_01_MainToolbar()
    {
        await NavigateAndWait();
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("01-main-toolbar"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("01-main-toolbar")), Is.True);
    }

    [Test, Order(2)]
    public async Task Capture_02_GitPlugin()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".git-branch")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("02-git-plugin"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("02-git-plugin")), Is.True);
    }

    [Test, Order(3)]
    public async Task Capture_03_ActionDeck()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".action-button").First).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("03-action-deck"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("03-action-deck")), Is.True);
    }

    [Test, Order(4)]
    public async Task Capture_04_TimeReportModal()
    {
        await NavigateAndWait();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("04-time-report-modal"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("04-time-report-modal")), Is.True);
    }

    [Test, Order(5)]
    public async Task Capture_05_TerminalOutput()
    {
        await NavigateAndWait();
        await Page.Locator(".action-button", new() { HasText = "Build Script" }).ClickAsync();
        await Expect(Page.Locator(".terminal-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("05-terminal-output"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("05-terminal-output")), Is.True);
    }

    [Test, Order(6)]
    public async Task Capture_06_SettingsPage()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("06-settings-page"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("06-settings-page")), Is.True);
    }

    [Test, Order(7)]
    public async Task Capture_07_ProjectSwitchDevOps()
    {
        await NavigateAndWait();
        // Switch to DevOps Pipeline project — wait for plugins to update
        await SwitchProject("DevOps Pipeline");
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("07-project-switch-devops"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("07-project-switch-devops")), Is.True);
    }

    [Test, Order(8)]
    public async Task Capture_08_ProjectSwitchFrontEnd()
    {
        await NavigateAndWait();
        // Switch to FrontEnd App project (3 enabled plugins: git-tools, work-items, time-tracker)
        await SwitchProject("FrontEnd App");
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("08-project-switch-frontend"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("08-project-switch-frontend")), Is.True);
    }

    [Test, Order(9)]
    public async Task Capture_09_TimeReportByProject()
    {
        await NavigateAndWait();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });
        // Switch to "This Week" and "By Project"
        await Page.Locator(".time-report-filter").First.SelectOptionAsync("week");
        await Page.Locator(".time-report-filter").Nth(1).SelectOptionAsync("project");
        await Expect(Page.Locator(".time-report-group-key").First).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("09-time-report-by-project"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("09-time-report-by-project")), Is.True);
    }

    [Test, Order(10)]
    public async Task Capture_10_SettingsConfigHierarchy()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        // Wait for the configuration file section (avoid ScrollIntoViewIfNeeded due to SSR DOM replacement)
        await Expect(Page.Locator("[data-testid='settings-config-section']")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("10-settings-config-hierarchy"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("10-settings-config-hierarchy")), Is.True);
    }

    [Test, Order(11)]
    public async Task Capture_11_PillToolbarPreview()
    {
        await NavigateAndWait();
        // Verify pill toolbar modules are visible
        await Expect(Page.Locator(".module-project")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(Page.Locator(".module-git")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("11-pill-toolbar-preview"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("11-pill-toolbar-preview")), Is.True);
    }

    [Test, Order(12)]
    public async Task Capture_12_TimeReportByDescription()
    {
        await NavigateAndWait();
        // Open time report
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-modal")).ToBeVisibleAsync(new() { Timeout = 5000 });
        // Switch to week view and group by description
        await Page.Locator(".time-report-filter").First.SelectOptionAsync("week");
        await Page.Locator(".time-report-filter").Nth(1).SelectOptionAsync("action");
        await Expect(Page.Locator(".time-report-group-key").First).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("12-time-report-by-description"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("12-time-report-by-description")), Is.True);
    }

    [Test, Order(13)]
    public async Task Capture_13_SettingsThemeEditor()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.Locator(".settings-color-picker")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("13-settings-theme-editor"), FullPage = true });
        Assert.That(File.Exists(ScreenshotPath("13-settings-theme-editor")), Is.True);
    }

    [Test, Order(14)]
    public async Task Capture_14_GitRepositoryAndWorktreeMenu()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".toolbar-shell[data-interactive='true']")).ToHaveCountAsync(1, new() { Timeout = 30000 });
        if ((await Page.Locator(".project-name").TextContentAsync())?.Trim() != "MyWebAPI")
            await SwitchProject("MyWebAPI");
        await Page.GetByTestId("git-selector").ClickAsync();
        await Expect(Page.GetByTestId("git-selector-menu").Locator(".git-worktree-link")).ToHaveCountAsync(3, new() { Timeout = 5000 });
        await Page.WaitForTimeoutAsync(400); // end of the opening animation
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("14-git-repository-worktree-menu"), FullPage = true });
        await Page.Keyboard.PressAsync("Escape");
        Assert.That(File.Exists(ScreenshotPath("14-git-repository-worktree-menu")), Is.True);
    }

    [Test, Order(15)]
    public async Task Capture_15_ModuleRightClickMenu()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".toolbar-shell[data-interactive='true']")).ToHaveCountAsync(1, new() { Timeout = 30000 });
        var git = Page.Locator(".module-frame[data-module-id='git-tools']");
        await git.ClickAsync(new() { Button = MouseButton.Right, Position = new() { X = 20, Y = 10 } });
        await Expect(git.GetByTestId("module-menu")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.WaitForTimeoutAsync(400); // end of the opening animation
        await Page.ScreenshotAsync(new() { Path = ScreenshotPath("15-module-menu"), FullPage = true });
        await Page.Keyboard.PressAsync("Escape");
        Assert.That(File.Exists(ScreenshotPath("15-module-menu")), Is.True);
    }
}
