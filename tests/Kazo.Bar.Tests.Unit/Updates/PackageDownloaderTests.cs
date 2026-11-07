using System.Net;
using System.Security.Cryptography;
using Kazo.Bar.Updates;

namespace Kazo.Bar.Tests.Unit.Updates;

public class PackageDownloaderTests
{
    private string _folder = null!;

    [SetUp]
    public void SetUp() => _folder = Directory.CreateTempSubdirectory("kazo-download-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public async Task Downloads_and_verifies_the_sha256_digest()
    {
        var bytes = "package content"u8.ToArray();
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var asset = new ReleaseAsset("Kazo.Bar-0.2.0-win-x64.zip", new Uri("https://example.test/p.zip"), bytes.Length, digest);
        var reports = new List<double>();
        var target = Path.Combine(_folder, "sub", asset.Name);

        await PackageDownloader.DownloadAsync(new HttpClient(new BytesHandler(bytes)), asset, target, new SyncProgress(reports));

        Assert.That(File.ReadAllBytes(target), Is.EqualTo(bytes));
        Assert.That(File.Exists(target + ".part"), Is.False);
        Assert.That(reports.Last(), Is.EqualTo(1.0));
    }

    [Test]
    public void Rejects_a_corrupted_download_and_leaves_no_file()
    {
        var asset = new ReleaseAsset("p.zip", new Uri("https://example.test/p.zip"), 3, "sha256:" + new string('0', 64));
        var target = Path.Combine(_folder, "p.zip");

        Assert.ThrowsAsync<InvalidDataException>(() =>
            PackageDownloader.DownloadAsync(new HttpClient(new BytesHandler([1, 2, 3])), asset, target));
        Assert.That(Directory.GetFiles(_folder), Is.Empty);
    }

    [Test]
    public void Accepts_a_release_without_digest()
    {
        var file = Path.Combine(_folder, "f");
        File.WriteAllText(file, "x");

        Assert.DoesNotThrow(() => PackageDownloader.VerifyDigest(file, null));
        Assert.DoesNotThrow(() => PackageDownloader.VerifyDigest(file, "md5:whatever"));
    }

    [Test]
    public void Http_errors_are_thrown()
    {
        var asset = new ReleaseAsset("p.zip", new Uri("https://example.test/p.zip"), 3, null);

        Assert.ThrowsAsync<HttpRequestException>(() =>
            PackageDownloader.DownloadAsync(new HttpClient(new BytesHandler([], HttpStatusCode.NotFound)), asset, Path.Combine(_folder, "p.zip")));
    }

    private sealed class BytesHandler(byte[] bytes, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(bytes) });
    }

    private sealed class SyncProgress(List<double> reports) : IProgress<double>
    {
        public void Report(double value) => reports.Add(value);
    }
}
