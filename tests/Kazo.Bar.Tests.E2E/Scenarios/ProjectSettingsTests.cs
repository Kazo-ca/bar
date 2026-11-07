namespace Kazo.Bar.Tests.E2E.Scenarios;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
/// E2E tests for the project settings page (/project-settings): display name, folder,
/// branch, theme and removal. Runs its own Kazo.Bar.Web instance because it changes projects.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class ProjectSettingsTests : PageTest
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

    private async Task<ILocator> OpenProjectSettingsAsync(string projectId)
    {
        await Page.GotoAsync($"{BaseUrl}/project-settings/{projectId}", new() { WaitUntil = WaitUntilState.NetworkIdle });
        var form = Page.GetByTestId("project-settings-form");
        await Expect(form).ToBeVisibleAsync(new() { Timeout = 30000 });
        return form;
    }

    /// <summary>Waits for the circuit so that typing reaches the component.</summary>
    private async Task FillAsync(string testId, string value)
    {
        var input = Page.GetByTestId(testId);
        await input.FillAsync(value);
        await Expect(input).ToHaveValueAsync(value);
    }

    [Test, Order(1)]
    public async Task EditLink_InSettings_OpensTheActiveProject()
    {
        await Page.GotoAsync($"{BaseUrl}/settings", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.GetByTestId("settings-edit-project")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.GetByTestId("settings-project-edit")).ToHaveCountAsync(3);

        await Page.GetByTestId("settings-edit-project").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/project-settings/proj-webapi$"));
        await Expect(Page.GetByTestId("project-settings-name")).ToHaveValueAsync("MyWebAPI");
        await Expect(Page.GetByTestId("project-settings-branch")).ToHaveValueAsync("develop");
        await Expect(Page.GetByTestId("project-settings-save")).ToBeDisabledAsync();
    }

    [Test, Order(2)]
    public async Task InvalidValues_ShowFieldErrors_AndAreNotSaved()
    {
        await OpenProjectSettingsAsync("proj-frontend");
        await Expect(Page.GetByTestId("project-settings-theme")).ToHaveValueAsync("green");

        await FillAsync("project-settings-name", "");
        await FillAsync("project-settings-color", "#12");
        await FillAsync("project-settings-branch", "bad branch");
        await Expect(Page.GetByTestId("project-settings-dirty")).ToBeVisibleAsync();
        await Page.GetByTestId("project-settings-save").ClickAsync();

        await Expect(Page.GetByTestId("project-settings-error-Name")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("project-settings-error-AccentColor")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("project-settings-error-DefaultBranch")).ToBeVisibleAsync();

        // Nothing was saved: reopening shows the stored values.
        await OpenProjectSettingsAsync("proj-frontend");
        await Expect(Page.GetByTestId("project-settings-name")).ToHaveValueAsync("FrontEnd App");
    }

    [Test, Order(3)]
    public async Task Save_RenamesTheActiveProject_AndAppliesItsThemeToTheToolbar()
    {
        await OpenProjectSettingsAsync("proj-webapi");

        await FillAsync("project-settings-name", "Public API");
        await Page.GetByTestId("project-settings-theme").SelectOptionAsync("dark");
        await FillAsync("project-settings-color", "#AA3300");
        await Expect(Page.GetByTestId("project-settings-preview")).ToContainTextAsync("Public API");
        await Page.GetByTestId("project-settings-save").ClickAsync();
        await Expect(Page.GetByTestId("project-settings-dirty")).ToHaveCountAsync(0);

        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.GetByTestId("project-selector")).ToContainTextAsync("Public API", new() { Timeout = 30000 });
        await Expect(Page.Locator(".toolbar-shell")).ToHaveAttributeAsync("data-theme", "dark");
        await Expect(Page.Locator(".toolbar-shell")).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex("#AA3300", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    [Test, Order(4)]
    public async Task ProjectMenu_OpensTheSettingsOfTheActiveProject()
    {
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.Locator(".toolbar-shell[data-interactive='true']")).ToHaveCountAsync(1, new() { Timeout = 30000 });

        await Page.GetByTestId("project-selector").ClickAsync();
        await Page.GetByTestId("project-dropdown-edit").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/project-settings/proj-webapi$"));
        await Expect(Page.GetByTestId("project-settings-form")).ToBeVisibleAsync();
    }

    [Test, Order(5)]
    public async Task Remove_DeletesTheProject_AndReturnsToSettings()
    {
        await OpenProjectSettingsAsync("proj-devops");

        await Page.GetByTestId("project-settings-remove").ClickAsync();
        await Page.GetByTestId("project-settings-remove-confirm").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/settings$"));
        await Expect(Page.Locator(".settings-project-row")).ToHaveCountAsync(2, new() { Timeout = 30000 });
        await Expect(Page.Locator(".settings-project-row", new() { HasText = "DevOps Pipeline" })).ToHaveCountAsync(0);
    }

    [Test, Order(6)]
    public async Task UnknownProject_ShowsAMessage()
    {
        await Page.GotoAsync($"{BaseUrl}/project-settings/missing", new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(Page.GetByTestId("project-settings-missing")).ToBeVisibleAsync(new() { Timeout = 30000 });
    }
}
