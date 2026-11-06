namespace Kazo.Bar.Tests.E2E.Scenarios;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
/// E2E tests for the Git repository selector and the plugin configuration window.
/// Runs its own Kazo.Bar.Web instance (mock services, in-memory state).
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
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "Kazo.Bar.Web"));

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
        Assert.Fail($"Kazo.Bar.Web did not start on {BaseUrl}.");
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
        await Expect(Page.GetByTestId("project-selector")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.Locator(".toolbar-shell[data-interactive='true']")).ToHaveCountAsync(1, new() { Timeout = 30000 });
    }

    private async Task<ILocator> OpenGitMenuAsync()
    {
        await Page.GetByTestId("git-selector").ClickAsync();
        var menu = Page.GetByTestId("git-selector-menu");
        await Expect(menu).ToBeVisibleAsync();
        return menu;
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

        var menu = await OpenGitMenuAsync();
        await Expect(menu.Locator(".git-selector-name")).ToHaveTextAsync(new[] { "my-webapi", "shared-lib" });
        await Expect(Page.GetByTestId("git-repo-name")).ToHaveTextAsync("my-webapi");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(menu).ToHaveCountAsync(0);
    }

    [Test]
    public async Task GitSelector_SwitchingRepository_UpdatesBranch()
    {
        await OpenToolbarAsync();
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("develop");

        var menu = await OpenGitMenuAsync();
        await menu.Locator(".git-selector-item", new() { HasText = "shared-lib" }).ClickAsync();

        await Expect(Page.GetByTestId("git-repo-name")).ToHaveTextAsync("shared-lib");
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("main");
        await Expect(Page.GetByTestId("git-worktree")).ToHaveCountAsync(0);

        // Back to the first repository for the other tests (the web host keeps its state).
        menu = await OpenGitMenuAsync();
        await menu.Locator(".git-selector-item", new() { HasText = "my-webapi" }).ClickAsync();
        await Expect(Page.GetByTestId("git-repo-name")).ToHaveTextAsync("my-webapi");
    }

    [Test]
    public async Task GitSelector_WorktreeLinks_SwitchTheModuleToTheWorktree()
    {
        await OpenToolbarAsync();
        // The sample repository has two linked worktrees: the current one is shown.
        await Expect(Page.GetByTestId("git-worktree")).ToContainTextAsync("my-webapi");

        var menu = await OpenGitMenuAsync();
        var links = menu.Locator(".git-worktree-link");
        await Expect(links).ToHaveCountAsync(3);
        await links.Filter(new() { HasText = "my-webapi-hotfix" }).ClickAsync();

        await Expect(Page.GetByTestId("git-repo-name")).ToHaveTextAsync("my-webapi");
        await Expect(Page.GetByTestId("git-worktree")).ToContainTextAsync("my-webapi-hotfix");
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("hotfix/login-redirect");
        await Expect(Page.Locator(".git-status")).ToContainTextAsync("2");

        menu = await OpenGitMenuAsync();
        await menu.Locator(".git-worktree-link", new() { HasText = "(main)" }).ClickAsync();
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("develop");
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
        var menu = await OpenGitMenuAsync();
        await Expect(menu.Locator(".git-selector-item", new() { HasText = "e2e-added-repo" })).ToHaveCountAsync(1);
        await Page.Keyboard.PressAsync("Escape");

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

        await Page.Locator(".plugin-settings-nav-item[data-plugin-id='time-tracker']").ClickAsync();
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
    [Test]
    public async Task WorkItemsWindow_GitHubSource_SavesTokenWithoutShowingIt()
    {
        await OpenGitSettingsAsync();
        await Page.Locator(".plugin-settings-nav-item[data-plugin-id='work-items']").ClickAsync();
        var repo = Page.Locator(".provider-repo").First;

        await repo.GetByTestId("work-items-provider").SelectOptionAsync("GitHub");
        await repo.GetByTestId("github-repository").FillAsync("not a repo");
        await repo.GetByTestId("provider-save").ClickAsync();
        await Expect(repo.GetByTestId("provider-message")).ToHaveClassAsync("plugin-settings-error");

        await repo.GetByTestId("github-repository").FillAsync("contoso/my-webapi");
        await repo.GetByTestId("provider-save").ClickAsync();
        await Expect(repo.GetByTestId("provider-message")).ToHaveTextAsync("Saved.");

        await repo.GetByTestId("secret-field-input").FillAsync("ghp_secret_value");
        await repo.GetByTestId("secret-field-save").ClickAsync();
        await Expect(repo.GetByTestId("secret-field-saved")).ToBeVisibleAsync();
        await Expect(Page.Locator("input[type=password]")).ToHaveCountAsync(0);
        Assert.That(await Page.ContentAsync(), Does.Not.Contain("ghp_secret_value"));

        // Restore for other tests.
        await repo.Locator(".secret-field-remove").ClickAsync();
        await Expect(repo.GetByTestId("secret-field-input")).ToBeVisibleAsync();
        await repo.GetByTestId("work-items-provider").SelectOptionAsync("");
        await Expect(repo.GetByTestId("secret-field")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task WorkItemsWindow_SignInWithGitHub_SavesTheSignInInsteadOfAToken()
    {
        // US2.4: without a client ID the button is off and says where to add it.
        await OpenGitSettingsAsync();
        await Page.Locator(".plugin-settings-nav-item[data-plugin-id='work-items']").ClickAsync();
        var repo = Page.Locator(".provider-repo").First;
        await repo.GetByTestId("work-items-provider").SelectOptionAsync("GitHub");
        await Expect(repo.GetByTestId("secret-field-signin")).ToBeDisabledAsync();
        await Expect(repo.GetByTestId("secret-field-signin-hint")).ToContainTextAsync("Settings → Sign-in apps");
        await Page.Locator(".plugin-settings-close").ClickAsync();

        var clientId = Page.GetByTestId("oauth-github-client-id");
        try
        {
            await clientId.FillAsync("Ov23liTestClient");
            await clientId.PressAsync("Tab");
            await OpenGitSettingsAsync();
            await Page.Locator(".plugin-settings-nav-item[data-plugin-id='work-items']").ClickAsync();
            repo = Page.Locator(".provider-repo").First;

            await repo.GetByTestId("secret-field-signin").ClickAsync();

            await Expect(repo.GetByTestId("secret-field-code")).ToHaveTextAsync("WDJB-MJHT");
            await Expect(repo.GetByTestId("secret-field-device").Locator("a")).ToHaveAttributeAsync("href", "https://github.com/login/device");
            await Expect(repo.GetByTestId("secret-field-signed-in")).ToHaveTextAsync("✓ Signed in with GitHub", new() { Timeout = 10000 });
            await Expect(repo.GetByTestId("secret-field-device")).ToHaveCountAsync(0);
        }
        finally
        {
            // Restore for other tests.
            if (await repo.Locator(".secret-field-remove").CountAsync() > 0)
                await repo.Locator(".secret-field-remove").ClickAsync();
            await repo.GetByTestId("work-items-provider").SelectOptionAsync("");
            await Page.Locator(".plugin-settings-close").ClickAsync();
            await clientId.FillAsync("");
            await clientId.PressAsync("Tab");
        }
        await Expect(clientId).ToHaveValueAsync("");
    }

    [Test]
    public async Task Settings_CiPollingInterval_IsEditableAndClampedToMinimum()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.NetworkIdle });
        var field = Page.GetByTestId("settings-ci-polling");
        await Expect(field).ToHaveValueAsync("60", new() { Timeout = 30000 });

        await field.FillAsync("10");
        await field.PressAsync("Tab");
        await Expect(field).ToHaveValueAsync("30");

        await field.FillAsync("120");
        await field.PressAsync("Tab");
        await Expect(field).ToHaveValueAsync("120");

        await field.FillAsync("60");
        await field.PressAsync("Tab");
        await Expect(field).ToHaveValueAsync("60");
    }
}
