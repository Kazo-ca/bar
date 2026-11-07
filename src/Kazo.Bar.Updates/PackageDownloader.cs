using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Kazo.Bar.Updates;

/// <summary>Downloads a release package and checks it against GitHub's SHA-256 digest.</summary>
public static class PackageDownloader
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Downloads <paramref name="asset"/> to <paramref name="destination"/> (via a <c>.part</c> file) and verifies it.
    /// Progress goes from 0 to 1 when the size is known.
    /// </summary>
    public static async Task DownloadAsync(
        HttpClient http,
        ReleaseAsset asset,
        string destination,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        var partial = destination + ".part";

        using (var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl))
        {
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Kazo-Bar-Updater", "1.0"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? asset.Size;

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            var buffer = new byte[BufferSize];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;
                if (total > 0) progress?.Report(Math.Min(1.0, (double)received / total));
            }
        }

        try
        {
            VerifyDigest(partial, asset.Digest);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
        File.Move(partial, destination, overwrite: true);
        progress?.Report(1.0);
    }

    /// <summary>
    /// Checks <c>sha256:&lt;hex&gt;</c> digests. A missing digest (older releases) is accepted;
    /// an unknown algorithm is accepted too, since there is nothing to compare with.
    /// </summary>
    /// <exception cref="InvalidDataException">The file does not match the digest.</exception>
    public static void VerifyDigest(string file, string? digest)
    {
        const string prefix = "sha256:";
        if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;

        var expected = digest[prefix.Length..].Trim();
        using var stream = File.OpenRead(file);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Downloaded file {Path.GetFileName(file)} is corrupted (SHA-256 {actual.ToLowerInvariant()}, expected {expected}).");
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
