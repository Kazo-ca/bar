namespace Kazo.Bar.Tests.E2E.Drivers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// Fixture that hosts the Kazo.Bar.Web application for Playwright E2E tests.
/// </summary>
public class WebAppFixture : IAsyncDisposable
{
    private WebApplicationFactory<Program>? _factory;
    public string BaseUrl { get; private set; } = string.Empty;

    public async Task StartAsync()
    {
        var port = GetAvailablePort();
        BaseUrl = $"http://localhost:{port}";

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseUrls(BaseUrl);
                builder.UseEnvironment("Development");
            });

        // Create an HTTP client to ensure the host is started
        _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    private static int GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
