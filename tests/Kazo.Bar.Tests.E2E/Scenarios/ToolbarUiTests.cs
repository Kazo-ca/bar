namespace Kazo.Bar.Tests.E2E.Scenarios;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
/// E2E tests for the Kazo Bar UI using Playwright.
/// Tests run against the Kazo.Bar.Web project hosted on localhost.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class ToolbarUiTests : PageTest
{
    private const string BaseUrl = "http://localhost:5280";
    private System.Diagnostics.Process? _serverProcess;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Start the web server
        var projectPath = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..",
                "src", "Kazo.Bar.Web"));

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

    /// <summary>
    /// Navigate to the app and wait for SSR content to appear.
    /// Ensures the default project (MyWebAPI) is selected.
    /// </summary>
    private async Task NavigateAndWait()
    {
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.Load });
        // The SSR-rendered content includes .toolbar-shell immediately
        await Expect(Page.Locator(".toolbar-shell")).ToBeVisibleAsync(new() { Timeout = 30000 });
        // Wait for the interactive (SignalR) render: clicks on prerendered HTML are lost
        await Expect(Page.Locator(".toolbar-shell[data-interactive='true']")).ToBeVisibleAsync(new() { Timeout = 30000 });
        // Ensure MyWebAPI is the active project (may have been changed by other test classes)
        var projectName = await Page.Locator(".project-name").TextContentAsync();
        if (projectName?.Trim() != "MyWebAPI")
        {
            await SwitchProject("MyWebAPI");
        }
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

    [Test]
    public async Task PageTitle_ShouldBeKazoBar()
    {
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page).ToHaveTitleAsync("Kazo Bar", new() { Timeout = 30000 });
    }

    [Test]
    public async Task ToolbarShell_HasProjectModule()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".module-project")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ProjectSelector_ShouldHaveThreeProjects()
    {
        await NavigateAndWait();
        // The menu lists every project, the active one checked
        await Page.Locator(".project-selector-trigger").ClickAsync();
        await Expect(Page.Locator(".project-dropdown")).ToBeVisibleAsync(new() { Timeout = 3000 });
        await Expect(Page.Locator(".project-dropdown-item[role='menuitemradio']")).ToHaveCountAsync(3);
        await Expect(Page.Locator(".project-dropdown-item.active")).ToContainTextAsync("MyWebAPI");
    }

    [Test]
    public async Task ProjectSelector_DefaultShouldBeMyWebAPI()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".project-name")).ToContainTextAsync("MyWebAPI");
    }

    [Test]
    public async Task GitPlugin_ShouldShowBranchAndStatus()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("develop");
        await Expect(Page.Locator(".git-status")).ToContainTextAsync("Clean");
    }

    [Test]
    public async Task ActionDeck_ShouldDisplayFourButtons()
    {
        await NavigateAndWait();
        // MyWebAPI has 4 actions: 1 SmartProcessButton (VS) + 3 regular action buttons
        var totalButtons = Page.Locator(".action-deck").Locator("button");
        await Expect(totalButtons).ToHaveCountAsync(4);
    }

    [Test]
    public async Task ActionDeck_ShouldShowVisualStudioButton()
    {
        await NavigateAndWait();
        // Visual Studio has WindowTitleRegex, so it renders as SmartProcessButton
        await Expect(Page.Locator(".smart-process-btn", new() { HasText = "Visual Studio" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task MyWebAPI_ShouldShowGitPluginPanel()
    {
        await NavigateAndWait();
        // Only Git plugin is rendered in the toolbar
        await Expect(Page.Locator(".module-git .plugin-panel")).ToHaveCountAsync(1);
    }

    // --- Settings Page Tests ---

    [Test]
    public async Task SettingsLink_ShouldBeVisible()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".system-settings")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SettingsPage_ShouldShowActiveProject()
    {
        // First ensure MyWebAPI is active
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.Locator(".settings-value", new() { HasText = "MyWebAPI" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task SettingsPage_ShouldShowPluginsList()
    {
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        // Scope to the plugin list: the Toolbar Behavior section reuses the same row style.
        await Expect(Page.Locator(".settings-plugins-list .settings-plugin-row")).ToHaveCountAsync(4);
    }

    [Test]
    public async Task SettingsPage_ShouldShowActionsList()
    {
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.Locator(".settings-action-row")).ToHaveCountAsync(4);
    }

    [Test]
    public async Task SettingsPage_ShouldShowAllProjects()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.Locator(".settings-project-row")).ToHaveCountAsync(3);
    }

    [Test]
    public async Task SettingsPage_ShouldHaveBackLink()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.Locator(".settings-back")).ToBeVisibleAsync();
        await Expect(Page.Locator(".settings-back")).ToContainTextAsync("Back to Toolbar");
    }

    [Test]
    public async Task SettingsPage_QuitButton_AsksForConfirmation()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });

        await Page.GetByTestId("settings-quit").ClickAsync();

        await Expect(Page.GetByTestId("settings-quit-confirm")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("settings-quit-status")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SettingsPage_QuitCancel_RestoresQuitButton()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });

        await Page.GetByTestId("settings-quit").ClickAsync();
        await Page.GetByTestId("settings-quit-cancel").ClickAsync();

        await Expect(Page.GetByTestId("settings-quit")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("settings-quit-confirm")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SettingsPage_QuitConfirm_RequestsApplicationExit()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });

        await Page.GetByTestId("settings-quit").ClickAsync();
        await Page.GetByTestId("settings-quit-confirm").ClickAsync();

        // The web host cannot close the browser; the mock native service records the request.
        await Expect(Page.GetByTestId("settings-quit-status")).ToContainTextAsync("Closing Kazo Bar");
    }

    // --- Git Quick Sync Tests ---

    [Test]
    public async Task GitPlugin_ShouldShowPullButton()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".git-btn-pull")).ToBeVisibleAsync();
        await Expect(Page.Locator(".git-btn-pull")).ToContainTextAsync("Pull");
    }

    [Test]
    public async Task GitPlugin_ShouldShowPushButton()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".git-btn-push")).ToBeVisibleAsync();
        await Expect(Page.Locator(".git-btn-push")).ToContainTextAsync("Push");
    }

    // --- Time Report Modal Tests ---

    [Test]
    public async Task TimeReportButton_ShouldBeVisible()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".system-btn[title='Time Report']")).ToBeVisibleAsync();
    }

    [Test]
    public async Task TimeReport_ShouldOpenOnClick()
    {
        await NavigateAndWait();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Time Report" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task TimeReport_ShouldHavePeriodFilter()
    {
        await NavigateAndWait();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(Page.Locator(".time-report-filter").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task TimeReport_ShouldHaveExportButton()
    {
        await NavigateAndWait();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(Page.Locator(".time-report-export")).ToBeVisibleAsync();
        await Expect(Page.Locator(".time-report-export")).ToContainTextAsync("Export CSV");
    }

    // --- Script / Terminal Output Tests ---

    [Test]
    public async Task ActionDeck_ShouldShowBuildScriptButton()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".action-button", new() { HasText = "Build Script" })).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task BuildScript_ShouldOpenTerminalOnClick()
    {
        await NavigateAndWait();
        var buildBtn = Page.Locator(".action-button", new() { HasText = "Build Script" });
        await Expect(buildBtn).ToBeVisibleAsync(new() { Timeout = 5000 });
        await buildBtn.ClickAsync();
        await Expect(Page.Locator(".terminal-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Terminal Output" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Terminal_ShouldShowMockOutput()
    {
        await NavigateAndWait();
        var buildBtn = Page.Locator(".action-button", new() { HasText = "Build Script" });
        await Expect(buildBtn).ToBeVisibleAsync(new() { Timeout = 5000 });
        await buildBtn.ClickAsync();
        await Expect(Page.Locator(".terminal-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(Page.Locator(".terminal-output")).ToContainTextAsync("Build successful");
    }

    [Test]
    public async Task Terminal_ShouldShowExitCode()
    {
        await NavigateAndWait();
        var buildBtn = Page.Locator(".action-button", new() { HasText = "Build Script" });
        await Expect(buildBtn).ToBeVisibleAsync(new() { Timeout = 5000 });
        await buildBtn.ClickAsync();
        await Expect(Page.Locator(".terminal-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(Page.Locator(".terminal-exit")).ToContainTextAsync("Exit: 0");
    }

    // --- Project Switching Tests ---

    [Test]
    public async Task ProjectSwitch_ShouldChangeVisiblePlugins()
    {
        await NavigateAndWait();
        // Default MyWebAPI has git plugin visible in module-git
        await Expect(Page.Locator(".module-git .plugin-panel")).ToHaveCountAsync(1);

        // Switch to DevOps Pipeline (has git-tools enabled)
        await SwitchProject("DevOps Pipeline");
        await Expect(Page.Locator(".module-git .plugin-panel")).ToHaveCountAsync(1, new() { Timeout = 5000 });

        // Switch back to MyWebAPI
        await SwitchProject("MyWebAPI");
        await Expect(Page.Locator(".module-git .plugin-panel")).ToHaveCountAsync(1, new() { Timeout = 5000 });
    }

    [Test]
    public async Task ProjectSwitch_ShouldUpdateActions()
    {
        await NavigateAndWait();
        // Default MyWebAPI has 4 actions (1 smart + 3 regular)
        var deckButtons = Page.Locator(".action-deck").Locator("button");
        await Expect(deckButtons).ToHaveCountAsync(4);

        // Switch to FrontEnd App (has 3 actions: 1 smart Browser + 2 regular)
        await SwitchProject("FrontEnd App");
        await Expect(deckButtons).ToHaveCountAsync(3, new() { Timeout = 5000 });
    }

    // --- Project Name Display Tests ---

    [Test]
    public async Task ProjectName_ShouldDisplayInSelector()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".project-selector-trigger")).ToBeVisibleAsync();
        await Expect(Page.Locator(".project-name")).ToContainTextAsync("MyWebAPI");
    }

    [Test]
    public async Task ProjectName_ShouldUpdateOnProjectSwitch()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".project-name")).ToContainTextAsync("MyWebAPI");

        // Switch to DevOps Pipeline
        await SwitchProject("DevOps Pipeline");
        await Expect(Page.Locator(".project-name")).ToContainTextAsync("DevOps Pipeline", new() { Timeout = 5000 });
    }

    // --- CSV Export Tests ---

    [Test]
    public async Task TimeReport_ExportButton_ShouldBeClickable()
    {
        await NavigateAndWait();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });

        var exportBtn = Page.Locator(".time-report-export");
        await Expect(exportBtn).ToBeVisibleAsync();
        await Expect(exportBtn).ToBeEnabledAsync();
    }

    // --- System Zone Tests ---

    [Test]
    public async Task SystemZone_ShouldShowTimerDisplay()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".system-timer-display")).ToBeVisibleAsync();
        await Expect(Page.Locator(".system-timer-icon")).ToBeVisibleAsync();
        await Expect(Page.Locator(".system-timer-value")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SystemZone_SettingsButton_OpensSettingsPageInHostsWithoutWindows()
    {
        await NavigateAndWait();
        var settingsButton = Page.Locator(".system-settings");
        await Expect(settingsButton).ToBeVisibleAsync();

        // The Web host has no native windows: the Settings dialog falls back to the /settings page.
        await settingsButton.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/settings$"));
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SystemZone_ShouldShowReportButton()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".system-btn[title='Time Report']")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ModuleActions_ShouldBeVisible()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".module-actions")).ToBeVisibleAsync();
    }

    // --- Git Default Branch from Config Tests (US5.1/US2.2) ---

    [Test]
    public async Task GitPlugin_ShouldChangeOnProjectSwitch()
    {
        await NavigateAndWait();
        // MyWebAPI has DefaultBranch = "develop"
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("develop");

        // Switch to DevOps Pipeline (DefaultBranch = "release/1.0")
        await SwitchProject("DevOps Pipeline");
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("release/1.0", new() { Timeout = 5000 });
    }

    // --- Time Report Grouping Tests (US5.4) ---

    [Test]
    public async Task TimeReport_ShouldHaveGroupByFilter()
    {
        await NavigateAndWait();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });
        // Should have two filters: period and groupBy
        await Expect(Page.Locator(".time-report-filter")).ToHaveCountAsync(2);
    }

    // --- Settings Page Enhancements (US2.2) ---

    [Test]
    public async Task SettingsPage_ShouldShowDefaultBranch()
    {
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.Locator(".settings-value", new() { HasText = "develop" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task SettingsPage_ShouldShowRepositoryLink()
    {
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.Locator(".settings-link")).ToBeVisibleAsync();
        await Expect(Page.Locator(".settings-link")).ToContainTextAsync("github.com");
    }

    // --- Settings Page Toggle Tests ---

    [Test]
    public async Task SettingsPage_ShouldShowPluginToggleSwitches()
    {
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
        // Scope to the plugin list: the Toolbar Behavior section also has toggle switches.
        await Expect(Page.Locator(".settings-plugins-list .settings-toggle")).ToHaveCountAsync(4);
    }

    [Test]
    public async Task SettingsPage_ShouldAllowProjectSwitching()
    {
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });

        // Click on a different project
        var frontendRow = Page.Locator(".settings-project-row", new() { HasText = "FrontEnd App" });
        await Expect(frontendRow).ToBeVisibleAsync();
        await frontendRow.ClickAsync();

        // Verify it becomes active
        await Expect(Page.Locator(".settings-value", new() { HasText = "FrontEnd App" })).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    // --- US5.4: Time Report By Project Grouping ---

    [Test]
    public async Task TimeReport_ShouldHaveByProjectGrouping()
    {
        await NavigateAndWait();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });

        // The groupBy filter should have "By Project" option
        var groupBySelect = Page.Locator(".time-report-filter").Nth(1);
        await Expect(groupBySelect.Locator("option", new() { HasText = "By Project" })).ToBeAttachedAsync();
    }

    [Test]
    public async Task TimeReport_ShouldShowPrePopulatedEntries()
    {
        await NavigateAndWait();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Should show entries with a non-zero total
        await Expect(Page.Locator(".time-report-total-value")).Not.ToHaveTextAsync("00:00:00");
    }

    [Test]
    public async Task TimeReport_ByProjectGrouping_ShouldShowProjectNames()
    {
        await NavigateAndWait();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Switch to "This Week" to get cross-project data
        await Page.Locator(".time-report-filter").First.SelectOptionAsync("week");
        // Select "By Project" grouping
        await Page.Locator(".time-report-filter").Nth(1).SelectOptionAsync("project");

        // Should show project group headers
        await Expect(Page.Locator(".time-report-group-key", new() { HasText = "MyWebAPI" })).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    // --- US2.3: single user configuration file shown in Settings ---

    [Test]
    public async Task SettingsPage_ShouldShowSingleConfigurationFile()
    {
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });

        await Expect(Page.Locator("[data-testid='settings-config-section']")).ToBeVisibleAsync();
        await Expect(Page.Locator("[data-testid='settings-config-path']")).ToContainTextAsync("config.json");
        await Expect(Page.Locator("text=Configuration Hierarchy")).ToHaveCountAsync(0);
    }

    // --- US5.1: a project is created linked to a repository folder ---

    [Test]
    public async Task SettingsPage_NewProjectForm_ValidatesTheFolder()
    {
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });

        await Page.Locator("[data-testid='settings-new-project']").ClickAsync();
        await Expect(Page.Locator("[data-testid='new-project-form']")).ToBeVisibleAsync();
        await Expect(Page.Locator("[data-testid='new-project-create']")).ToBeDisabledAsync();

        await Page.Locator("[data-testid='new-project-path']").FillAsync("C:\\bad\"folder");
        await Page.Locator("[data-testid='new-project-create']").ClickAsync();

        await Expect(Page.Locator("[data-testid='new-project-error']")).ToContainTextAsync("invalid characters");
    }

    // --- US2.4: choice of the access token store ---

    [Test]
    public async Task SettingsPage_SecretStore_CanBeChanged()
    {
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });

        var vault = Page.Locator("[data-testid='secret-store-CredentialManager']");
        var file = Page.Locator("[data-testid='secret-store-EncryptedFile']");
        await Expect(vault).ToBeCheckedAsync();

        await file.CheckAsync();
        await Expect(file).ToBeCheckedAsync();

        // Restore the shared Web host state for the other tests.
        await vault.CheckAsync();
        await Expect(vault).ToBeCheckedAsync();
    }

    // ==============================================================
    // MAUI Desktop Preview Chrome Tests
    // ==============================================================

    [Test]
    public async Task PillDesign_ShouldShowProjectModule()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".module-project")).ToBeVisibleAsync();
    }

    [Test]
    public async Task PillDesign_ShouldShowGitModule()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".module-git")).ToBeVisibleAsync();
    }

    [Test]
    public async Task PillDesign_ShouldShowSystemModule()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".module-system")).ToBeVisibleAsync();
    }

    [Test]
    public async Task PillDesign_ShouldShowPreviewHint()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".maui-preview-hint")).ToContainTextAsync("Web preview");
    }

    [Test]
    public async Task PillDesign_ToolbarRendersAsPill()
    {
        await NavigateAndWait();
        await Expect(Page.Locator(".toolbar-preview-frame .toolbar-shell")).ToBeVisibleAsync();
    }

    [Test]
    public async Task PillDesign_SettingsPageRendersInPreview()
    {
        await NavigateAndWait();
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".toolbar-preview-frame")).ToBeVisibleAsync();
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
    }

    // ===== US5.4: Description Grouping Test =====

    [Test]
    public async Task TimeReport_HasDescriptionGroupingOption()
    {
        await NavigateAndWait();

        // Open time report
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-modal")).ToBeVisibleAsync(new() { Timeout = 5000 });

        // The grouping dropdown should have a "By Description" option
        var groupBySelect = Page.Locator(".time-report-filter").Nth(1);
        await Expect(groupBySelect.Locator("option[value='action']")).ToBeAttachedAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task TimeReport_ByDescriptionGrouping_ShowsTagHeaders()
    {
        await NavigateAndWait();

        // Open time report with "week" period to get sample entries
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-modal")).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Switch to week view
        var periodSelect = Page.Locator(".time-report-filter").First;
        await periodSelect.SelectOptionAsync("week");

        // Select "By Description" grouping
        var groupBySelect = Page.Locator(".time-report-filter").Nth(1);
        await groupBySelect.SelectOptionAsync("action");

        // Should show grouped entries with 🏷 tag headers
        // Wait for the re-render: reading the text at once can still see the previous grouping's header.
        await Expect(Page.Locator(".time-report-group-key").First).ToContainTextAsync("🏷", new() { Timeout = 5000 });
    }

    // ============================================================================
    // US4.2: SmartProcessButton in ActionDeck
    // ============================================================================

    [Test]
    public async Task SmartProcessButton_RendersForActionsWithWindowTitleRegex()
    {
        await NavigateAndWait();

        // Visual Studio has WindowTitleRegex, should render as SmartProcessButton
        var smartBtn = Page.Locator(".smart-process-btn");
        await Expect(smartBtn.First).ToBeVisibleAsync(new() { Timeout = 5000 });
        var text = await smartBtn.First.TextContentAsync();
        Assert.That(text, Does.Contain("Visual Studio"), "SmartProcessButton should show Visual Studio");
    }

    [Test]
    public async Task SmartProcessButton_RegularActionsStillRenderAsButtons()
    {
        await NavigateAndWait();

        // Postman has no WindowTitleRegex, should render as regular button
        var regularBtn = Page.Locator(".action-button:has-text('Postman')");
        await Expect(regularBtn).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    // ============================================================================
    // US3.2: Theme Editor in Settings
    // ============================================================================

    [Test]
    public async Task Settings_ThemeEditor_ColorPickerVisible()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });

        // Color picker should be visible
        var colorPicker = Page.Locator(".settings-color-picker");
        await Expect(colorPicker).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task Settings_ThemeEditor_FontSelectVisible()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });

        // Font family select should be visible with multiple options
        var fontSelect = Page.Locator(".settings-font-select");
        await Expect(fontSelect).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Should have multiple font options
        var options = fontSelect.Locator("option");
        var count = await options.CountAsync();
        Assert.That(count, Is.GreaterThanOrEqualTo(4), "Font selector should have at least 4 font options");
    }

    // ============================================================================
    // US5.1: Git Sync (Pull/Push buttons exist and are clickable)
    // ============================================================================

    [Test]
    public async Task GitPlugin_PullPushButtons_AreVisible()
    {
        await NavigateAndWait();

        // Both Pull and Push buttons should be visible and enabled
        var pullBtn = Page.Locator(".git-btn-pull");
        var pushBtn = Page.Locator(".git-btn-push");
        await Expect(pullBtn).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(pushBtn).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(pullBtn).ToBeEnabledAsync(new() { Timeout = 5000 });
        await Expect(pushBtn).ToBeEnabledAsync(new() { Timeout = 5000 });

        // Verify button text
        var pullText = await pullBtn.TextContentAsync();
        var pushText = await pushBtn.TextContentAsync();
        Assert.That(pullText, Does.Contain("Pull"), "Pull button should say Pull");
        Assert.That(pushText, Does.Contain("Push"), "Push button should say Push");
    }

    // ── Popup dismissal (shared by Web, MAUI and WPF hosts) ──

    private async Task OpenProjectDropdown()
    {
        await NavigateAndWait();
        await Page.Locator(".project-selector-trigger").ClickAsync();
        await Expect(Page.Locator(".project-dropdown")).ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    [Test]
    public async Task ProjectDropdown_ShouldCloseOnOutsideClick()
    {
        await OpenProjectDropdown();
        await Page.Locator(".system-timer-display").ClickAsync();
        await Expect(Page.Locator(".project-dropdown")).ToHaveCountAsync(0, new() { Timeout = 3000 });
    }

    [Test]
    public async Task ProjectDropdown_ShouldCloseOnEscapeAndRefocusTrigger()
    {
        await OpenProjectDropdown();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(".project-dropdown")).ToHaveCountAsync(0, new() { Timeout = 3000 });
        await Expect(Page.Locator(".project-selector-trigger")).ToBeFocusedAsync(new() { Timeout = 3000 });
    }

    [Test]
    public async Task ProjectDropdown_ShouldCloseWhenAnotherPopupOpens()
    {
        await OpenProjectDropdown();
        await Page.Locator(".test-dropdown-wrapper .system-btn").ClickAsync();
        await Expect(Page.Locator(".test-dropdown-panel")).ToBeVisibleAsync(new() { Timeout = 3000 });
        await Expect(Page.Locator(".project-dropdown")).ToHaveCountAsync(0, new() { Timeout = 3000 });
    }

    [Test]
    public async Task ProjectDropdown_ClickInsideShouldKeepItOpen()
    {
        await OpenProjectDropdown();
        // Top padding, away from the 16 px rounded corners: a corner point can fall outside the shape.
        await Page.Locator(".project-dropdown").ClickAsync(new() { Position = new() { X = 100, Y = 2 } });
        await Expect(Page.Locator(".project-dropdown")).ToBeVisibleAsync();
    }
}
