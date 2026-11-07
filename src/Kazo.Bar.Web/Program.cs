using Kazo.Bar.Core.Events;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Plugins.Git;
using Kazo.Bar.Plugins.GithubAgents;
using Kazo.Bar.Plugins.Services;
using Kazo.Bar.Plugins.TimeTracker;
using Kazo.Bar.Plugins.WorkItems;
using Kazo.Bar.UI.Services;
using Kazo.Bar.Web.Mocks;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register mock services for web testing
builder.Services.AddSingleton<IProcessService, MockProcessService>();
builder.Services.AddSingleton<IScriptService, MockScriptService>();
builder.Services.AddSingleton<IFileSystemService, MockFileSystemService>();
builder.Services.AddSingleton<ISettingsService, MockSettingsService>();
builder.Services.AddSingleton<IStorageService, MockStorageService>();
builder.Services.AddSingleton<INativeService, MockNativeService>();
builder.Services.AddSingleton<IWindowService, MockWindowService>();
builder.Services.AddSingleton<IDockingService, MockDockingService>();
builder.Services.AddSingleton<IGlobalSettingsService, MockGlobalSettingsService>();
builder.Services.AddSingleton<IWindowRegionService, MockWindowRegionService>();
builder.Services.AddSingleton<MockSecretStore>();
builder.Services.AddSingleton<ISecretStore>(sp => sp.GetRequiredService<MockSecretStore>());
builder.Services.AddSingleton<ISecretStoreSelector>(sp => sp.GetRequiredService<MockSecretStore>());
builder.Services.AddSingleton<IOAuthSignInService, MockOAuthSignInService>();

// Register plugins
builder.Services.AddSingleton<MockGitService>();
builder.Services.AddSingleton<IGitService>(sp => sp.GetRequiredService<MockGitService>());
builder.Services.AddSingleton<IGitRepositoryManager>(sp => sp.GetRequiredService<MockGitService>());
builder.Services.AddSingleton<IPlugin, GitPlugin>();
builder.Services.AddSingleton<IWorkItemProvider, MockWorkItemProvider>();
builder.Services.AddSingleton<IPlugin, WorkItemsPlugin>();
builder.Services.AddSingleton<ITimeTrackingService, MockTimeTrackingService>();
builder.Services.AddSingleton<IPlugin, TimeTrackerPlugin>();
builder.Services.AddSingleton<ICiCdService, MockCiCdService>();
builder.Services.AddSingleton<IPlugin, GithubAgentPlugin>();
builder.Services.AddSingleton<IPluginLoader, PluginLoader>();

// Register UI services
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<EventAggregator>();
// No native windows in a browser: dialogs are overlays in the page (one service per circuit).
builder.Services.AddScoped<Kazo.Bar.Core.Dialogs.IDialogService, Kazo.Bar.UI.Dialogs.InPageDialogService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<Kazo.Bar.Web.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Kazo.Bar.UI.Pages.Home).Assembly);

app.Run();

public partial class Program { }
