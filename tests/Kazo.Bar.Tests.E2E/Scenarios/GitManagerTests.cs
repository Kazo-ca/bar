namespace Kazo.Bar.Tests.E2E.Scenarios;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
/// E2E tests for the Git repository manager (US5.7, US5.8): +/- counter, changes and diff,
/// commit, branches and worktrees. Runs its own Kazo.Bar.Web instance (simulated Git, in-memory
/// state); each test leaves the toolbar on the repository's main folder.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class GitManagerTests : PageTest
{
    private const string BaseUrl = "http://localhost:5282";
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

    public override BrowserNewContextOptions ContextOptions() => new() { ViewportSize = new() { Width = 1200, Height = 780 } };

    private async Task OpenToolbarAsync()
    {
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.Locator(".toolbar-shell[data-interactive='true']")).ToHaveCountAsync(1, new() { Timeout = 30000 });
    }

    private async Task<ILocator> OpenGitMenuAsync()
    {
        await Page.GetByTestId("git-selector").ClickAsync();
        var menu = Page.GetByTestId("git-selector-menu");
        await Expect(menu).ToBeVisibleAsync();
        return menu;
    }

    private async Task SelectWorktreeAsync(string text)
    {
        var menu = await OpenGitMenuAsync();
        await menu.Locator(".git-worktree-link", new() { HasText = text }).ClickAsync();
    }

    private async Task<ILocator> OpenManagerAsync(string tab)
    {
        var menu = await OpenGitMenuAsync();
        await menu.GetByTestId($"git-manage-{tab}").ClickAsync();
        var manager = Page.GetByTestId("git-manager");
        await Expect(manager).ToBeVisibleAsync();
        return manager;
    }

    [Test]
    public async Task CleanFolder_HasNoCounter_AndTheMenuOpensTheManager()
    {
        await OpenToolbarAsync();
        await Expect(Page.GetByTestId("git-changes-counter")).ToHaveCountAsync(0);

        var manager = await OpenManagerAsync("changes");
        await Expect(manager.GetByTestId("git-no-changes")).ToBeVisibleAsync();
        await Expect(manager.GetByTestId("git-commit")).ToBeDisabledAsync();

        await Page.Keyboard.PressAsync("Escape");
        await Expect(manager).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Counter_OpensChanges_ShowsTheDiff_StagesAndCommits()
    {
        await OpenToolbarAsync();
        await SelectWorktreeAsync("my-webapi-hotfix");
        var counter = Page.GetByTestId("git-changes-counter");
        await Expect(counter).ToContainTextAsync("+12");
        await Expect(counter).ToContainTextAsync("−2");

        await counter.ClickAsync();
        var manager = Page.GetByTestId("git-manager");
        await Expect(manager.GetByTestId("git-file-row")).ToHaveCountAsync(2);

        // The first file is selected: its diff shows removed and added lines.
        var diff = manager.GetByTestId("git-diff");
        await Expect(diff).ToContainTextAsync("LoginController.cs");
        await Expect(diff.Locator(".git-diff-line.added")).ToHaveCountAsync(3);
        await Expect(diff.Locator(".git-diff-line.deleted")).ToHaveCountAsync(2);

        // Another file: a new file shows all its lines as added.
        await manager.Locator("[data-testid='git-file-row'][data-path='src/Auth/RedirectGuard.cs']").ClickAsync();
        await Expect(diff).ToContainTextAsync("RedirectGuard");
        await Expect(diff.Locator(".git-diff-line.deleted")).ToHaveCountAsync(0);

        // Stage one file, then commit only it.
        var row = manager.Locator("[data-testid='git-file-row'][data-path='src/Auth/LoginController.cs']");
        await row.HoverAsync();
        await row.GetByRole(AriaRole.Button, new() { Name = "Stage" }).ClickAsync();
        await Expect(manager.GetByTestId("git-staged").GetByTestId("git-file-row")).ToHaveCountAsync(1);
        await Expect(manager.GetByTestId("git-commit")).ToContainTextAsync("Commit 1 staged");

        await manager.GetByTestId("git-commit-message").FillAsync("Fix the login redirect");
        await manager.GetByTestId("git-commit").ClickAsync();
        await Expect(manager.GetByTestId("git-manager-message")).ToContainTextAsync("Fix the login redirect");
        await Expect(manager.GetByTestId("git-file-row")).ToHaveCountAsync(1);
        await Expect(manager.GetByTestId("git-commit-message")).ToHaveValueAsync("");
        await Expect(counter).ToContainTextAsync("+9");

        // Commit all: nothing staged, so every change goes in.
        await manager.GetByTestId("git-commit-message").FillAsync("Add the redirect guard");
        await manager.GetByTestId("git-commit").ClickAsync();
        await Expect(manager.GetByTestId("git-no-changes")).ToBeVisibleAsync();
        await Expect(counter).ToHaveCountAsync(0);

        await manager.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await SelectWorktreeAsync("(main)");
    }

    [Test]
    public async Task Branches_CreateSwitchAndDelete()
    {
        await OpenToolbarAsync();
        var manager = await OpenManagerAsync("branches");

        await manager.GetByTestId("git-new-branch-name").FillAsync("feature/e2e branch");
        await Expect(manager.Locator(".git-form-error")).ToContainTextAsync("spaces");
        await manager.GetByTestId("git-new-branch-name").FillAsync("feature/e2e");
        await manager.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await Expect(manager.Locator(".git-branch-row.current")).ToContainTextAsync("feature/e2e");
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("feature/e2e");

        var develop = manager.Locator("[data-testid='git-branch-row'][data-branch='develop']");
        await develop.GetByTestId("git-checkout").ClickAsync();
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("develop");

        var feature = manager.Locator("[data-testid='git-branch-row'][data-branch='feature/e2e']");
        await feature.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await feature.GetByRole(AriaRole.Button, new() { Name = "Delete?" }).ClickAsync();
        await Expect(manager.Locator("[data-testid='git-branch-row'][data-branch='feature/e2e']")).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync("Escape");
    }

    [Test]
    public async Task Worktrees_ChangeTheLocation_CreateFromASourceBranch_AndDelete()
    {
        await OpenToolbarAsync();
        var manager = await OpenManagerAsync("worktrees");
        await Expect(manager.GetByTestId("git-worktree-row")).ToHaveCountAsync(3);
        await Expect(manager.GetByTestId("git-worktree-location-path")).ToHaveTextAsync("/projects/my-webapi.worktrees");

        await manager.GetByTestId("git-worktree-location-change").ClickAsync();
        await manager.GetByTestId("git-worktree-location-input").FillAsync("/work/trees");
        await manager.GetByTestId("git-worktree-location-save").ClickAsync();
        await Expect(manager.GetByTestId("git-worktree-location-path")).ToHaveTextAsync("/work/trees");

        await manager.GetByTestId("git-new-worktree-source").SelectOptionAsync("origin/release/2.0");
        await manager.GetByTestId("git-new-worktree-branch").FillAsync("fix/release-crash");
        await Expect(manager.GetByTestId("git-new-worktree-folder")).ToHaveValueAsync("/work/trees/fix-release-crash");
        await manager.GetByTestId("git-new-worktree-create").ClickAsync();

        await Expect(manager.GetByTestId("git-worktree-row")).ToHaveCountAsync(4);
        await Expect(Page.GetByTestId("git-worktree")).ToContainTextAsync("fix-release-crash");
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("fix/release-crash");

        var created = manager.Locator("[data-testid='git-worktree-row'][data-path='/work/trees/fix-release-crash']");
        await created.GetByTestId("git-worktree-remove").ClickAsync();
        await created.GetByTestId("git-worktree-remove-confirm").ClickAsync();
        await Expect(manager.GetByTestId("git-worktree-row")).ToHaveCountAsync(3);
        // The deleted worktree was the active one: the toolbar is back on the repository.
        await Expect(Page.Locator(".git-branch")).ToContainTextAsync("develop");

        await Page.Keyboard.PressAsync("Escape");
    }
}
