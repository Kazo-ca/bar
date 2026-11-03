using DevToolbar.Core.Events;
using DevToolbar.Core.Interfaces;
using DevToolbar.Plugins.Git;
using DevToolbar.Plugins.GithubAgents;
using DevToolbar.Plugins.Services;
using DevToolbar.Plugins.TimeTracker;
using DevToolbar.Plugins.WorkItems;
using DevToolbar.UI.Services;
using DevToolbar.Web.Mocks;

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

// Register plugins
builder.Services.AddSingleton<IGitService, MockGitService>();
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<DevToolbar.Web.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(DevToolbar.UI.Pages.Home).Assembly);

app.Run();

public partial class Program { }
