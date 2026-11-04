namespace DevToolbar.Tests.E2E.Scenarios;

using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
/// Behaviour tests derived from the acceptance criteria in doc/Features.md.
/// Each test names the user story it covers. The web host keeps its mock services
/// as singletons for the whole run, so every test restores any state it changes.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class FeatureSpecTests : PageTest
{
    private const string BaseUrl = "http://localhost:5280";
    private const int ServerStartRetries = 60;
    private System.Diagnostics.Process? _serverProcess;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
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

        using var httpClient = new HttpClient();
        for (var i = 0; i < ServerStartRetries; i++)
        {
            try
            {
                var response = await httpClient.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // Server not ready yet
            }
            await Task.Delay(1000);
        }
        Assert.Fail($"Web host did not start on {BaseUrl}");
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

    // ───────────────────────── helpers ─────────────────────────

    private async Task OpenToolbar(string project = "MyWebAPI")
    {
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.Load });
        await Expect(Page.Locator(".toolbar-shell")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await WaitForInteractive();
        var current = (await Page.Locator(".project-name").TextContentAsync())?.Trim();
        if (current != project)
        {
            await SwitchProject(project);
        }
    }

    /// <summary>Clicks on prerendered HTML are lost; wait for the interactive render.</summary>
    private Task WaitForInteractive() =>
        Expect(Page.Locator(".toolbar-shell[data-interactive='true']")).ToBeVisibleAsync(new() { Timeout = 30000 });

    private async Task SwitchProject(string projectName)
    {
        await Page.Locator(".project-selector-trigger").ClickAsync();
        await Expect(Page.Locator(".project-dropdown")).ToBeVisibleAsync(new() { Timeout = 3000 });
        await Page.Locator(".project-dropdown-item", new() { HasText = projectName }).ClickAsync();
        await Expect(Page.Locator(".project-name")).ToContainTextAsync(projectName, new() { Timeout = 5000 });
    }

    private ILocator Popover(string pluginId) => Page.Locator($".plugin-popover[data-plugin-id='{pluginId}']");

    private async Task OpenPopover(string pluginId)
    {
        await Popover(pluginId).Locator(".plugin-popover-trigger").ClickAsync();
        await Expect(Popover(pluginId).Locator(".plugin-popover-panel")).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    /// <summary>Clicks the empty page corner, away from the toolbar and any open panel.</summary>
    private Task ClickOutsideToolbar() => Page.Mouse.ClickAsync(5, 5);

    private ILocator Toast(string text) => Page.Locator(".toast", new() { HasText = text });

    private async Task OpenSettings()
    {
        await Page.Locator(".system-settings").ClickAsync();
        await Expect(Page.Locator(".settings-page")).ToBeVisibleAsync(new() { Timeout = 30000 });
    }

    private async Task BackToToolbar()
    {
        await Page.Locator(".settings-back").ClickAsync();
        await Expect(Page.Locator(".toolbar-shell")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await WaitForInteractive();
    }

    // ───────────────────────── F3 · Shell & dropdowns ─────────────────────────

    [Test]
    public async Task US3_4_ActionsThatDoNotFit_MoveToTheOverflowMenu()
    {
        var original = Page.ViewportSize;
        await Page.SetViewportSizeAsync(1600, 720);
        await OpenToolbar();
        await Expect(Page.GetByTestId("action-overflow")).ToHaveCountAsync(0);
        var all = await Page.Locator(".action-deck > .action-slot").CountAsync();

        try
        {
            // The WPF window is 960 px wide.
            await Page.SetViewportSizeAsync(960, 720);
            var trigger = Page.GetByTestId("action-overflow");
            await Expect(trigger).ToBeVisibleAsync(new() { Timeout = 5000 });
            var visible = await Page.Locator(".action-deck > .action-slot").CountAsync();

            await trigger.ClickAsync();
            var menu = Page.GetByTestId("action-overflow-menu");
            await Expect(menu).ToBeVisibleAsync();
            await Expect(menu.Locator(".action-overflow-item")).ToHaveCountAsync(all - visible);
            await Expect(menu).ToContainTextAsync("Build Script");

            await Page.Keyboard.PressAsync("Escape");
            await Expect(menu).ToHaveCountAsync(0);
        }
        finally
        {
            await Page.SetViewportSizeAsync(1600, 720);
        }
        await Expect(Page.GetByTestId("action-overflow")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        if (original != null)
            await Page.SetViewportSizeAsync(original.Width, original.Height);
    }

    [Test]
    public async Task US3_4_LongNames_AreTruncated_WithTheFullTextInTheTooltip()
    {
        await OpenToolbar();
        var name = Page.Locator(".project-name");
        await Expect(name).ToHaveCSSAsync("text-overflow", "ellipsis");
        await Expect(Page.Locator(".project-selector-trigger")).ToHaveAttributeAsync("title", new System.Text.RegularExpressions.Regex("^MyWebAPI"));
        await Expect(Page.Locator(".git-branch-name")).ToHaveCSSAsync("text-overflow", "ellipsis");
        var branch = (await Page.Locator(".git-branch-name").TextContentAsync())!;
        await Expect(Page.Locator(".git-branch")).ToHaveAttributeAsync("title", branch);
    }

    [Test]
    public async Task US3_6_GitModule_IsResizedByDraggingItsEdge_AndRightClickResetsTheWidth()
    {
        await OpenToolbar();
        var module = Page.Locator(".module-frame[data-module-id='git-tools']");
        var start = (await module.BoundingBoxAsync())!;
        var grip = module.GetByTestId("module-resize-grip");
        var box = (await grip.BoundingBoxAsync())!;

        try
        {
            await Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
            await Page.Mouse.DownAsync();
            await Page.Mouse.MoveAsync(box.X + 80, box.Y + box.Height / 2, new() { Steps = 5 });
            await Page.Mouse.UpAsync();
            await WaitForWidth(module, w => w > start.Width + 60);

            // Saved: still wider after a reload.
            await OpenToolbar();
            await WaitForWidth(module, w => w > start.Width + 60);

            await module.ClickAsync(new() { Button = MouseButton.Right, Position = new() { X = 20, Y = 10 } });
            var menu = module.GetByTestId("module-menu");
            await Expect(menu).ToBeVisibleAsync();
            await menu.GetByTestId("module-reset-width").ClickAsync();
            await Expect(menu).ToHaveCountAsync(0);
            await WaitForWidth(module, w => Math.Abs(w - start.Width) < 1);
        }
        finally
        {
            await ResetToolbarLayout();
        }
    }

    [Test]
    public async Task US3_7_ModulesAreReordered_FromTheirRightClickMenu()
    {
        await OpenToolbar();
        var git = Page.Locator(".module-frame[data-module-id='git-tools']");
        await Expect(Page.Locator(".toolbar-shell > .module-frame").First).ToHaveAttributeAsync("data-module-id", "git-tools");

        try
        {
            await git.ClickAsync(new() { Button = MouseButton.Right, Position = new() { X = 20, Y = 10 } });
            await Expect(git.GetByTestId("module-move-left")).ToBeDisabledAsync();
            await git.GetByTestId("module-move-right").ClickAsync();

            await Expect(Page.Locator(".toolbar-shell > .module-frame").First).ToHaveAttributeAsync("data-module-id", "core.actions");
            // Not right after the project pill any more: the Git module is a pill of its own.
            await Expect(git).Not.ToHaveCSSAsync("margin-left", "-18px");

            // System zone: the timer moves before the indicators.
            var timer = Page.Locator(".module-frame[data-module-id='core.timer']");
            await timer.ClickAsync(new() { Button = MouseButton.Right });
            await timer.GetByTestId("module-move-left").ClickAsync();
            var system = Page.Locator(".module-system > .module-frame");
            var count = await system.CountAsync();
            await Expect(system.Nth(count - 2)).ToHaveAttributeAsync("data-module-id", "core.timer");

            // The order is listed (and can be reset) in Settings.
            await Page.Locator(".system-settings").ClickAsync();
            var section = Page.GetByTestId("settings-modules-section");
            await Expect(section.Locator(".settings-module-row").First).ToHaveAttributeAsync("data-module-id", "core.actions");
            await section.GetByTestId("settings-reset-order").ClickAsync();
            await Expect(section.Locator(".settings-module-row").First).ToHaveAttributeAsync("data-module-id", "git-tools");
        }
        finally
        {
            await ResetToolbarLayout();
        }
        await OpenToolbar();
        await Expect(Page.Locator(".toolbar-shell > .module-frame").First).ToHaveAttributeAsync("data-module-id", "git-tools");
    }

    private static async Task WaitForWidth(ILocator element, Func<float, bool> expected)
    {
        float width = 0;
        for (var i = 0; i < 50; i++)
        {
            width = (await element.BoundingBoxAsync())?.Width ?? 0;
            if (expected(width)) return;
            await Task.Delay(100);
        }
        Assert.Fail($"Unexpected width: {width}px");
    }

    [Test]
    public async Task US3_8_ModulesThatDoNotFit_MoveToTheOverflowMenu_AndOpenTheirPanel()
    {
        await Page.SetViewportSizeAsync(1600, 720);
        await OpenToolbar();
        var trigger = Page.GetByTestId("module-overflow");
        await Expect(trigger).ToHaveCountAsync(0);

        try
        {
            await Page.SetViewportSizeAsync(600, 720);
            await Expect(trigger).ToBeVisibleAsync(new() { Timeout = 5000 });
            var hidden = Page.Locator(".module-system > .module-frame.overflowed");
            var count = await hidden.CountAsync();
            Assert.That(count, Is.GreaterThan(0));
            // The last modules of the zone leave first: the timer is the last one by default.
            await Expect(Page.Locator(".module-frame[data-module-id='core.timer']")).ToHaveClassAsync(new Regex("overflowed"));

            await trigger.ClickAsync();
            var menu = Page.GetByTestId("module-overflow-menu");
            await Expect(menu.GetByTestId("module-overflow-item")).ToHaveCountAsync(count);
            await menu.Locator("[data-module-id='core.timer']").ClickAsync();

            var panel = Page.GetByTestId("module-overflow-panel");
            await Expect(panel).ToBeVisibleAsync();
            await Expect(panel.Locator(".plugin-popover-title")).ToHaveTextAsync("Timer");
            await panel.Locator(".module-overflow-back").ClickAsync();
            await Expect(menu).ToBeVisibleAsync();

            await Page.Keyboard.PressAsync("Escape");
            await Expect(menu).ToHaveCountAsync(0);
        }
        finally
        {
            await Page.SetViewportSizeAsync(1600, 720);
        }
        await Expect(trigger).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Expect(Page.Locator(".module-frame.overflowed")).ToHaveCountAsync(0);
        await Page.SetViewportSizeAsync(1280, 720);
    }

    /// <summary>The web host keeps its settings for the whole run: put the default layout back.</summary>
    private async Task ResetToolbarLayout()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.Load });
        var section = Page.GetByTestId("settings-modules-section");
        await Expect(section).ToBeVisibleAsync(new() { Timeout = 30000 });
        foreach (var id in new[] { "settings-reset-order", "settings-reset-widths" })
        {
            var button = section.GetByTestId(id);
            // A click before the page is interactive is lost: retry until the reset is applied.
            for (var attempt = 0; attempt < 20 && await button.IsEnabledAsync(); attempt++)
            {
                await button.ClickAsync();
                await Page.WaitForTimeoutAsync(250);
            }
            await Expect(button).ToBeDisabledAsync();
        }
    }

    [Test]
    public async Task US3_1_ProjectDropdown_ClosesOnEscape()
    {
        await OpenToolbar();
        await Page.Locator(".project-selector-trigger").ClickAsync();
        await Expect(Page.Locator(".project-dropdown")).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(".project-dropdown")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".project-name")).ToHaveTextAsync("MyWebAPI");
    }

    [Test]
    public async Task US3_1_ProjectDropdown_ClosesOnOutsideClick()
    {
        await OpenToolbar();
        await Page.Locator(".project-selector-trigger").ClickAsync();
        await Expect(Page.Locator(".project-dropdown")).ToBeVisibleAsync();

        await ClickOutsideToolbar();

        await Expect(Page.Locator(".project-dropdown")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task US3_1_ProjectTrigger_ExposesExpandedState()
    {
        await OpenToolbar();
        var trigger = Page.Locator(".project-selector-trigger");
        await Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false");

        await trigger.ClickAsync();

        await Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");
        await Page.Keyboard.PressAsync("Escape");
    }

    [Test]
    public async Task US3_2_AccentColor_FollowsActiveProject()
    {
        await OpenToolbar();
        await Expect(Page.Locator(".toolbar-shell")).ToHaveAttributeAsync("style", new Regex("--accent-color: #0078D7"));

        await SwitchProject("DevOps Pipeline");

        await Expect(Page.Locator(".toolbar-shell")).ToHaveAttributeAsync("style", new Regex("--accent-color: #E53935"));
        await SwitchProject("MyWebAPI");
    }

    [Test]
    public async Task US3_2_CustomCss_IsInjectedOnlyForProjectsThatDefineIt()
    {
        await OpenToolbar();
        var style = Page.Locator("#devtoolbar-custom-css");
        await Expect(style).ToHaveCountAsync(0);

        await SwitchProject("DevOps Pipeline");

        await Expect(style).ToHaveCountAsync(1);
        var applied = await Page.Locator(".toolbar-shell").EvaluateAsync<string>(
            "el => getComputedStyle(el).getPropertyValue('--devops-custom-css').trim()");
        Assert.That(applied, Is.EqualTo("applied"));

        await SwitchProject("MyWebAPI");
        await Expect(style).ToHaveCountAsync(0);
    }

    [Test]
    public async Task US3_3_Theme_FollowsActiveProject()
    {
        await OpenToolbar();
        var shell = Page.Locator(".toolbar-shell");
        await Expect(shell).ToHaveAttributeAsync("data-theme", "light");
        var lightBackground = await ShellBackground();

        await SwitchProject("FrontEnd App");
        await Expect(shell).ToHaveAttributeAsync("data-theme", "green");
        var greenBackground = await ShellBackground();

        await SwitchProject("DevOps Pipeline");
        await Expect(shell).ToHaveAttributeAsync("data-theme", "dark");
        var darkBackground = await ShellBackground();

        Assert.That(new[] { lightBackground, greenBackground, darkBackground }.Distinct().Count(), Is.EqualTo(3),
            "each theme gives the toolbar its own glass colour");
        await SwitchProject("MyWebAPI");
    }

    [Test]
    public async Task US3_3_Theme_IsChosenInSettings_AndKeepsTheSameElements()
    {
        await OpenToolbar();
        var shell = Page.Locator(".toolbar-shell");
        await Expect(shell).ToHaveAttributeAsync("data-theme", "light");
        var lightElements = await ToolbarElementClasses();

        try
        {
            await OpenSettings();
            var select = Page.GetByTestId("theme-select");
            await Expect(select).ToHaveValueAsync("light");
            await select.SelectOptionAsync("dark");
            await BackToToolbar();

            await Expect(shell).ToHaveAttributeAsync("data-theme", "dark");
            Assert.That(await ToolbarElementClasses(), Is.EqualTo(lightElements), "a theme only changes styles");

            // The choice is saved with the project: it survives a project switch.
            await SwitchProject("FrontEnd App");
            await SwitchProject("MyWebAPI");
            await Expect(shell).ToHaveAttributeAsync("data-theme", "dark");
        }
        finally
        {
            await OpenSettings();
            await Page.GetByTestId("theme-select").SelectOptionAsync("light");
            await BackToToolbar();
        }

        await Expect(shell).ToHaveAttributeAsync("data-theme", "light");
    }

    private Task<string> ShellBackground() =>
        Page.Locator(".toolbar-shell").EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");

    /// <summary>The class list of every element in the toolbar, in document order.</summary>
    private Task<string[]> ToolbarElementClasses() =>
        Page.Locator(".toolbar-shell").EvaluateAsync<string[]>(
            "el => [...el.querySelectorAll('*')].map(e => e.tagName + '.' + [...e.classList].sort().join('.'))");

    // ───────────────────────── F4 · Quick actions ─────────────────────────

    [Test]
    public async Task US4_1_ProcessAction_ShowsSuccessToast()
    {
        await OpenToolbar();
        await Page.Locator(".action-button", new() { HasText = "Postman" }).ClickAsync();
        await Expect(Toast("Launched Postman")).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task US4_1_InvalidPath_ShowsErrorToast()
    {
        await OpenToolbar("DevOps Pipeline");
        await Page.Locator(".action-button", new() { HasText = "Legacy Tool" }).ClickAsync();

        var toast = Page.Locator(".toast-error", new() { HasText = "Failed to launch Legacy Tool" });
        await Expect(toast).ToBeVisibleAsync(new() { Timeout = 5000 });
        await SwitchProject("MyWebAPI");
    }

    [Test]
    public async Task US4_2_SmartFocus_FocusesExistingWindow()
    {
        await OpenToolbar();
        var button = Page.Locator(".smart-process-btn", new() { HasText = "Visual Studio" });

        await button.ClickAsync();

        await Expect(Toast("Focused Visual Studio")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(button.Locator(".smart-process-badge")).ToHaveTextAsync("Active");
    }

    [Test]
    public async Task US4_2_SmartFocus_LaunchesWhenNoWindowMatches()
    {
        await OpenToolbar("FrontEnd App");
        await Page.Locator(".smart-process-btn", new() { HasText = "Browser" }).ClickAsync();

        await Expect(Toast("Launched Browser")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await SwitchProject("MyWebAPI");
    }

    [Test]
    public async Task US4_2_SmartFocus_SeveralWindows_LetsTheUserChoose()
    {
        await OpenToolbar("DevOps Pipeline");
        var button = Page.Locator(".smart-process-btn", new() { HasText = "Azure Portal" });

        await button.ClickAsync();
        var menu = Page.Locator(".smart-process-menu");
        await Expect(menu).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(menu.Locator(".smart-process-menu-title")).ToHaveTextAsync("2 windows match");
        await Expect(menu.Locator(".smart-process-menu-item")).ToHaveCountAsync(3); // 2 windows + "Launch a new one"

        await Page.Keyboard.PressAsync("Escape");
        await Expect(menu).ToHaveCountAsync(0);

        await button.ClickAsync();
        await menu.Locator(".smart-process-menu-item", new() { HasText = "Azure DevOps Boards" }).ClickAsync();
        await Expect(menu).ToHaveCountAsync(0);
        await Expect(Toast("Focused Azure Portal")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(button.Locator(".smart-process-badge")).ToHaveTextAsync("Active");
        await SwitchProject("MyWebAPI");
    }

    [Test]
    public async Task US4_3_Terminal_StreamsLines_AndCanBeStopped()
    {
        await OpenToolbar();
        await Page.Locator(".action-button", new() { HasText = "Build Script" }).ClickAsync();

        // The first line is on screen while the script is still running.
        await Expect(Page.Locator(".terminal-line").First).ToContainTextAsync("[Mock] Executed", new() { Timeout = 5000 });
        var stop = Page.Locator(".terminal-stop");
        await Expect(stop).ToBeVisibleAsync();
        await stop.ClickAsync();

        await Expect(Page.Locator(".terminal-exit")).ToHaveTextAsync("Stopped", new() { Timeout = 5000 });
        await Expect(Page.Locator(".terminal-output")).Not.ToContainTextAsync("3 tests passed");
        await Expect(stop).ToHaveCountAsync(0);
        await Page.Locator(".terminal-close").ClickAsync();
    }

    [Test]
    public async Task US4_3_Terminal_ShowsCommandAndCloses()
    {
        await OpenToolbar();
        await Page.Locator(".action-button", new() { HasText = "Build Script" }).ClickAsync();
        await Expect(Page.Locator(".terminal-exit")).ToContainTextAsync("Exit: 0", new() { Timeout = 10000 });
        await Expect(Page.Locator(".terminal-title")).ToHaveTextAsync("pwsh build.ps1");

        await Page.Locator(".terminal-close").ClickAsync();

        await Expect(Page.Locator(".terminal-overlay.visible")).ToHaveCountAsync(0);
    }

    // ───────────────────────── F5 · Git (US5.1) ─────────────────────────

    [Test]
    public async Task US5_1_GitPull_ShowsSyncStatusInToolbar()
    {
        await OpenToolbar();
        await Page.Locator(".git-btn-pull").ClickAsync();

        await Expect(Page.Locator(".git-sync-status")).ToHaveTextAsync("✓ Pull complete", new() { Timeout = 5000 });
        await Expect(Toast("Pull completed")).ToBeVisibleAsync();
    }

    [Test]
    public async Task US5_1_GitPush_ShowsSyncStatusInToolbar()
    {
        await OpenToolbar();
        await Page.Locator(".git-btn-push").ClickAsync();

        await Expect(Page.Locator(".git-sync-status")).ToHaveTextAsync("✓ Push complete", new() { Timeout = 5000 });
    }

    // ───────────────────────── F5 · Work items (US5.2) ─────────────────────────

    [Test]
    public async Task US5_2_WorkItemIndicator_ShowsIdAndTitle()
    {
        await OpenToolbar();
        var trigger = Popover("work-items").Locator(".plugin-popover-trigger");

        await Expect(trigger.Locator(".system-indicator-text")).ToHaveTextAsync("#1234");
        await Expect(trigger).ToHaveAttributeAsync("title", "#1234 - Fix login page redirect");
    }

    [Test]
    public async Task US5_2_WorkItemLink_OpensProviderPageInNewTab()
    {
        await OpenToolbar();
        await OpenPopover("work-items");

        var link = Popover("work-items").Locator(".workitem-active .workitem-link");
        await Expect(link).ToHaveAttributeAsync("href", "https://github.com/example/my-webapi/issues/1234");
        await Expect(link).ToHaveAttributeAsync("target", "_blank");
        await Page.Keyboard.PressAsync("Escape");
    }

    [Test]
    public async Task US5_2_WorkItemSearch_SelectsAnotherItem()
    {
        await OpenToolbar();
        await OpenPopover("work-items");
        var popover = Popover("work-items");

        await popover.Locator(".workitem-search-toggle").ClickAsync();
        await popover.Locator(".workitem-search-input").PressSequentiallyAsync("dark");
        var result = popover.Locator(".workitem-dropdown-item", new() { HasText = "Add dark mode support" });
        await Expect(result).ToBeVisibleAsync(new() { Timeout = 5000 });
        await result.Locator(".workitem-dropdown-title").ClickAsync();

        await Expect(popover.Locator(".system-indicator-text")).ToHaveTextAsync("#1235");

        // Restore: a project switch reloads the default (first) work item.
        await Page.Keyboard.PressAsync("Escape");
        await SwitchProject("FrontEnd App");
        await SwitchProject("MyWebAPI");
        await Expect(popover.Locator(".system-indicator-text")).ToHaveTextAsync("#1234");
    }

    [Test]
    public async Task US5_2_WorkItemSearch_ShowsNoResultsMessage()
    {
        await OpenToolbar();
        await OpenPopover("work-items");
        var popover = Popover("work-items");

        await popover.Locator(".workitem-search-toggle").ClickAsync();
        await popover.Locator(".workitem-search-input").PressSequentiallyAsync("zzz-nothing");

        await Expect(popover.Locator(".workitem-no-results")).ToHaveTextAsync("No items found", new() { Timeout = 5000 });
        await popover.Locator(".workitem-search-toggle").ClickAsync();
        await Page.Keyboard.PressAsync("Escape");
    }

    [Test]
    public async Task US5_2_PluginPopover_ClosesOnEscapeAndOutsideClick()
    {
        await OpenToolbar();
        var panel = Popover("work-items").Locator(".plugin-popover-panel");

        await OpenPopover("work-items");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToHaveCountAsync(0);

        await OpenPopover("work-items");
        await ClickOutsideToolbar();
        await Expect(panel).ToHaveCountAsync(0);
    }

    [Test]
    public async Task US2_2_IndicatorsOnlyForPluginsEnabledInProject()
    {
        await OpenToolbar("DevOps Pipeline");

        // DevOps Pipeline enables git-tools + github-agents only
        await Expect(Popover("work-items")).ToHaveCountAsync(0);
        await Expect(Popover("time-tracker")).ToHaveCountAsync(0);
        await Expect(Popover("github-agents")).ToHaveCountAsync(1);
        await SwitchProject("MyWebAPI");
    }

    // ───────────────────────── F5 · Time tracking (US5.3 / US5.4) ─────────────────────────

    [Test]
    public async Task US5_3_TimerStartStop_LinksActiveWorkItem()
    {
        await OpenToolbar();
        await OpenPopover("time-tracker");
        var popover = Popover("time-tracker");

        await popover.Locator(".timer-btn-start").ClickAsync();
        try
        {
            await Expect(Page.Locator(".system-timer-display.running")).ToBeVisibleAsync(new() { Timeout = 5000 });
            await Expect(popover.Locator(".timer-status")).ToHaveTextAsync("⏺ Recording");
            await Expect(popover.Locator(".timer-workitem-id")).ToHaveTextAsync("#1234");
            // The panel's clock keeps ticking while recording
            await Expect(popover.Locator(".timer-duration")).Not.ToHaveTextAsync("00:00:00", new() { Timeout = 5000 });
        }
        finally
        {
            await popover.Locator(".timer-btn-stop").ClickAsync();
        }

        await Expect(Page.Locator(".system-timer-display.running")).ToHaveCountAsync(0);
        await Expect(popover.Locator(".timer-status")).ToHaveTextAsync("⏸ Stopped");
        await Page.Keyboard.PressAsync("Escape");
    }

    [Test]
    public async Task US5_3_TimerPanel_ShowsIdleTimeout()
    {
        await OpenToolbar();
        await OpenPopover("time-tracker");

        await Expect(Popover("time-tracker").Locator(".timer-idle-label")).ToHaveTextAsync("Idle timeout: 15 min");
        await Page.Keyboard.PressAsync("Escape");
    }

    [Test]
    public async Task US5_5_WorkDay_ShowsArrivalAndLastLock()
    {
        await OpenToolbar();
        await OpenPopover("time-tracker");
        var popover = Popover("time-tracker");

        // The Web host's sample day: session opened at 08:12, locked at 11:45.
        await Expect(popover.GetByTestId("workday-arrival")).ToHaveTextAsync("08:12");
        await Expect(popover.GetByTestId("workday-last-lock")).ToHaveTextAsync("11:45");
        await Page.Keyboard.PressAsync("Escape");
    }

    [Test]
    public async Task US5_5_AbsenceOnReturn_SplitIntoDailyAndLunch_AndLunchCanCountAsWork()
    {
        await OpenToolbar();
        // Away 11:45-13:00 with a daily 11:45-12:00: meeting then lunch, waiting for review.
        await Expect(Page.GetByTestId("absence-badge")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await OpenPopover("time-tracker");
        var review = Popover("time-tracker").GetByTestId("absence-review");
        await Expect(review).ToContainTextAsync("You were away 1 h 15 (11:45–13:00)");
        await Expect(review.Locator("[data-kind='Meeting'] .timer-absence-range")).ToHaveTextAsync("11:45–12:00");
        await Expect(review.Locator("[data-kind='Lunch'] .timer-absence-range")).ToHaveTextAsync("12:00–13:00");

        await review.Locator("[data-kind='Lunch'] .timer-absence-worked").ClickAsync();
        await Expect(review.Locator("[data-kind='Lunch']")).ToHaveCountAsync(0);

        await review.Locator("[data-kind='Meeting'] .timer-absence-save").ClickAsync();
        await Expect(Popover("time-tracker").GetByTestId("absence-review")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("absence-badge")).ToHaveCountAsync(0);
        await Page.Keyboard.PressAsync("Escape");

        // The report lists the work day with the labelled absences.
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        var days = Page.GetByTestId("report-workdays");
        await Expect(days).ToContainTextAsync("Meeting: Daily");
        await Expect(days).ToContainTextAsync("Worked: Worked through lunch");
        await Page.Keyboard.PressAsync("Escape");
    }

    [Test]
    public async Task US5_3_Settings_HasTimeTrackingRules()
    {
        await OpenToolbar();
        await OpenSettings();
        var section = Page.GetByTestId("settings-time-section");
        await Expect(section).ToBeVisibleAsync();
        await Expect(section.GetByTestId("tt-idle")).ToHaveValueAsync("15");
        await Expect(section.GetByTestId("tt-lunch-start")).ToHaveValueAsync("12:00");
        await Expect(section.GetByTestId("tt-meeting-start")).ToHaveValueAsync("11:45");
        await BackToToolbar();
    }

    [Test]
    public async Task US5_4_TimeReport_ClosesOnEscape()
    {
        await OpenToolbar();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Expect(Page.Locator(".time-report-overlay.visible")).ToBeVisibleAsync(new() { Timeout = 5000 });

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(".time-report-overlay.visible")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task US5_4_TimeReport_GroupByWorkItem_ShowsWorkItemHeaders()
    {
        await OpenToolbar();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Page.Locator(".time-report-filter").Nth(1).SelectOptionAsync("workitem");

        await Expect(Page.Locator(".time-report-group-key", new() { HasText = "#1234" })).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Page.Locator(".time-report-close").ClickAsync();
    }

    [Test]
    public async Task US5_4_TimeReport_CsvExport_ContainsTimesheetColumns()
    {
        await OpenToolbar();
        await Page.Locator(".system-btn[title='Time Report']").ClickAsync();
        await Page.Locator(".time-report-filter").First.SelectOptionAsync("week");

        var download = await Page.RunAndWaitForDownloadAsync(
            () => Page.Locator(".time-report-export").ClickAsync());
        var path = await download.PathAsync();
        var lines = await File.ReadAllLinesAsync(path!);

        Assert.That(download.SuggestedFilename, Does.Match(@"^time-report-week-\d{8}\.csv$"));
        Assert.That(lines[0], Is.EqualTo("Project,Date,Start Time,End Time,Duration,Work Item,Description"));
        Assert.That(lines.Any(l => l.StartsWith("MyWebAPI,") && l.Contains(",1234,Fix login redirect")), Is.True,
            "Expected a MyWebAPI row for work item 1234 with its description");
        await Page.Locator(".time-report-close").ClickAsync();
    }

    // ───────────────────────── F7 · CI/CD (US7.1 / US7.2) ─────────────────────────

    [Test]
    public async Task US7_1_CiCdIndicator_ShowsGlobalStatusInToolbar()
    {
        await OpenToolbar();
        var trigger = Popover("github-agents").Locator(".plugin-popover-trigger");

        // The mock has a failed deployment, so the global status icon shows "failure".
        await Expect(trigger).ToHaveClassAsync(new Regex(@"\bstatus-failure\b"));
        await Expect(trigger).ToHaveAttributeAsync("title", new Regex("^CI/CD"));
    }

    [Test]
    public async Task US7_2_ClickingSession_MarksItReadAndLinksToRun()
    {
        await OpenToolbar();
        await OpenPopover("github-agents");
        var session = Popover("github-agents").Locator(".cicd-session", new() { HasText = "Deploy to Staging" });

        await Expect(session.Locator(".cicd-session-name"))
            .ToHaveAttributeAsync("href", "https://github.com/example/my-webapi/actions/runs/102");
        await Expect(session.Locator(".cicd-session-name")).ToHaveAttributeAsync("target", "_blank");

        // Click the row (not the link) so no tab opens; the run must be marked read.
        await session.Locator(".cicd-session-branch").ClickAsync();

        await Expect(session).Not.ToHaveClassAsync(new Regex(@"\bunread\b"), new() { Timeout = 5000 });
        await Page.Keyboard.PressAsync("Escape");
    }

    [Test]
    public async Task US7_2_ReadAll_ClearsUnreadBadge()
    {
        await OpenToolbar();
        await OpenPopover("github-agents");
        var popover = Popover("github-agents");
        if (await popover.Locator(".cicd-mark-all-read").CountAsync() > 0)
        {
            await popover.Locator(".cicd-mark-all-read").ClickAsync();
        }

        await Expect(popover.Locator(".system-indicator-badge")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Expect(popover.Locator(".cicd-session.unread")).ToHaveCountAsync(0);
        await Page.Keyboard.PressAsync("Escape");
    }

    // ───────────────────────── Settings → toolbar integration ─────────────────────────

    [Test]
    public async Task US2_2_DisablingPluginInSettings_HidesItFromToolbar()
    {
        await OpenToolbar();
        await OpenSettings();
        var row = Page.Locator(".settings-plugins-list .settings-plugin-row", new() { HasText = "Work Items" });
        await row.Locator(".settings-toggle").ClickAsync();
        await Expect(row.Locator("input")).Not.ToBeCheckedAsync();

        try
        {
            await BackToToolbar();
            await Expect(Popover("work-items")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        }
        finally
        {
            await OpenSettings();
            await row.Locator(".settings-toggle").ClickAsync();
            await Expect(row.Locator("input")).ToBeCheckedAsync();
            await BackToToolbar();
        }
        await Expect(Popover("work-items")).ToHaveCountAsync(1, new() { Timeout = 5000 });
    }

    [Test]
    public async Task ShowActiveWindows_ListsWindowsAndActivatesOnClick()
    {
        await OpenToolbar();
        await OpenSettings();
        var toggleRow = Page.Locator(".settings-plugin-row", new() { HasText = "Show Active Windows" });
        await toggleRow.Locator(".settings-toggle").ClickAsync();
        await Expect(toggleRow.Locator("input")).ToBeCheckedAsync();

        try
        {
            await BackToToolbar();
            var windows = Page.Locator(".taskbar-window-btn");
            await Expect(windows).ToHaveCountAsync(3, new() { Timeout = 10000 });

            var postman = windows.Filter(new() { HasText = "Postman" });
            await postman.ClickAsync();
            await Expect(postman).ToHaveClassAsync(new Regex(@"\bactive\b"), new() { Timeout = 10000 });
        }
        finally
        {
            await OpenSettings();
            await toggleRow.Locator(".settings-toggle").ClickAsync();
            await Expect(toggleRow.Locator("input")).Not.ToBeCheckedAsync();
            await BackToToolbar();
        }
        await Expect(Page.Locator(".taskbar-window-btn")).ToHaveCountAsync(0);
    }
}
