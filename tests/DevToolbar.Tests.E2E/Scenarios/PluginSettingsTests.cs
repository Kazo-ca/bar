namespace DevToolbar.Tests.E2E.Scenarios;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
/// E2E tests for the Git repository selector and the plugin configuration window.
/// Runs its own DevToolbar.Web instance (mock services, in-memory state).
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class PluginSettingsTests : PageTest
{
    private const string BaseUrl = "http://localhost:5281";
    private System.Diagnostics.Process? _serverProcess;

    [OneTimeSetUp]
    public async Task StartServer()
    {
        var projectPath = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "DevToolbar.Web"));

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
        for (var i = 0; i < 90; i++)
        {
            try
            {
                if ((await httpClient.GetAsync(BaseUrl)).IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // Server not ready yet.
            }
            await Task.Delay(1000);
        }
        Assert.Fail($"DevToolbar.Web did not start on {BaseUrl}.");
    }

    [OneTimeTearDown]
    public void StopServer()
    {
        if (_serverProcess is { HasExited: false })
        {
            _serverProcess.Kill(entireProcessTree: true);
        }
        _serverProcess?.Dispose();
    }

    private async Task OpenToolbarAsync()
    {
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.Locator(".git-repo-select")).ToBeVisibleAsync(new() { Timeout = 30000 });
    }

    private async Task OpenGitSettingsAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.Locator(".settings-configure-all")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Page.Locator(".settings-plugin-row[data-plugin-id='git-tools'] .settings-plugin-configure").ClickAsync();
        await Expect(Page.Locator(".plugin-settings-window")).ToBeVisibleAsync();
    }

    [Test]
    public async Task GitSelector_ListsProjectRepositories()
    {
        await OpenToolbarAsync();

        await Expect(Page.Locator(".git-repo-select option")).ToContainTextAsync(new[] { "my-webapi", "shared-lib" });
    }

    [Test]
    public async Task GitSelector_SwitchingRepository_UpdatesBranch()
    {
        await OpenToolbarAsync();
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("develop");

        await Page.Locator(".git-repo-select").SelectOptionAsync("/projects/shared-lib");

        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("main");
    }

    [Test]
    public async Task PluginWindow_ListsAllPlugins_AndOpensOnRequestedPlugin()
    {
        await OpenGitSettingsAsync();

        await Expect(Page.Locator(".plugin-settings-nav-item")).ToHaveCountAsync(4);
        await Expect(Page.Locator(".plugin-settings-nav-item.selected")).ToContainTextAsync("Git Tools");
        await Expect(Page.Locator(".git-repo-row")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task PluginWindow_AddsAndRemovesRepository()
    {
        await OpenGitSettingsAsync();

        await Page.Locator(".git-repo-input-path").FillAsync("/projects/e2e-added-repo");
        await Page.Locator(".git-repo-input-branch").FillAsync("trunk");
        await Page.Locator(".git-repo-add").ClickAsync();

        var added = Page.Locator(".git-repo-row", new() { HasText = "e2e-added-repo" });
        await Expect(added).ToBeVisibleAsync();
        await Expect(added.Locator(".git-repo-branch")).ToContainTextAsync("trunk");

        await OpenToolbarAsync();
        await Expect(Page.Locator(".git-repo-select option", new() { HasText = "e2e-added-repo" })).ToHaveCountAsync(1);

        await OpenGitSettingsAsync();
        await Page.Locator(".git-repo-row", new() { HasText = "e2e-added-repo" }).Locator(".git-repo-remove").ClickAsync();
        await Expect(Page.Locator(".git-repo-row", new() { HasText = "e2e-added-repo" })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task PluginWindow_RejectsDuplicateFolder()
    {
        await OpenGitSettingsAsync();

        await Page.Locator(".git-repo-input-path").FillAsync("/projects/shared-lib/");
        await Page.Locator(".git-repo-add").ClickAsync();

        await Expect(Page.Locator(".plugin-settings-error")).ToContainTextAsync("already in the list");
    }

    [Test]
    public async Task PluginWindow_PluginWithoutSettings_ShowsHint_AndClosesOnEscape()
    {
        await OpenGitSettingsAsync();

        await Page.Locator(".plugin-settings-nav-item[data-plugin-id='work-items']").ClickAsync();
        await Expect(Page.Locator(".plugin-settings-none")).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(".plugin-settings-window")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task PluginWindow_EnableToggle_SyncsWithSettingsList()
    {
        await OpenGitSettingsAsync();
        await Page.Locator(".plugin-settings-nav-item[data-plugin-id='time-tracker']").ClickAsync();

        await Page.Locator(".plugin-settings-enable").ClickAsync();
        await Expect(Page.Locator(".plugin-settings-nav-item[data-plugin-id='time-tracker'] .plugin-settings-dot.off")).ToBeVisibleAsync();
        await Page.Locator(".plugin-settings-close").ClickAsync();
        await Expect(Page.Locator(".settings-plugin-row[data-plugin-id='time-tracker'] input")).Not.ToBeCheckedAsync();

        // Restore for other tests.
        await Page.Locator(".settings-plugin-row[data-plugin-id='time-tracker'] .settings-toggle-slider").ClickAsync();
        await Expect(Page.Locator(".settings-plugin-row[data-plugin-id='time-tracker'] input")).ToBeCheckedAsync();
    }
}
