namespace Kazo.Bar.Tests.Unit.Auth;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Infrastructure.Auth;

[TestFixture]
public class OAuthCredentialTests
{
    [TestCase("ghp_abc123")]
    [TestCase("52charsAzureDevOpsPersonalAccessTokenxxxxxxxxxxxxxx")]
    [TestCase("{ not json")]
    [TestCase("""{"kind":"something-else","accessToken":"x"}""")]
    [TestCase("""{"kind":"oauth","provider":"gitHub"}""")]
    [TestCase("")]
    public void TryParse_returns_false_for_anything_that_is_not_a_saved_sign_in(string value)
    {
        Assert.That(OAuthCredential.TryParse(value, out _), Is.False);
    }

    [Test]
    public void A_sign_in_with_a_refresh_token_saves_only_the_refresh_token()
    {
        // Arrange
        var tokens = new OAuthTokens("access-123", "refresh-456", DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var saved = OAuthCredential.From(OAuthProvider.AzureDevOps, tokens).Serialize();

        // Assert
        Assert.That(saved, Does.Contain("refresh-456").And.Not.Contain("access-123"));
        Assert.That(OAuthCredential.TryParse(saved, out var parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(new OAuthCredential(OAuthProvider.AzureDevOps, null, "refresh-456")));
    }

    [Test]
    public void A_sign_in_without_refresh_token_saves_the_access_token()
    {
        var saved = OAuthCredential.From(OAuthProvider.GitHub, new OAuthTokens("gho_abc", null, null)).Serialize();

        Assert.That(OAuthCredential.TryParse(saved, out var parsed), Is.True);
        Assert.That(parsed.AccessToken, Is.EqualTo("gho_abc"));
        Assert.That(parsed.Provider, Is.EqualTo(OAuthProvider.GitHub));
        Assert.That(saved, Does.StartWith("{").And.Contain("\"kind\":\"oauth\""));
    }

    [Test]
    public void A_saved_entra_sign_in_fits_in_credential_manager()
    {
        // Entra refresh tokens are about 1-2 KB; Credential Manager accepts 2,560 bytes.
        var refresh = new string('r', 2000);

        var saved = OAuthCredential.From(OAuthProvider.AzureDevOps, new OAuthTokens(new string('a', 3000), refresh, null)).Serialize();

        Assert.That(System.Text.Encoding.UTF8.GetByteCount(saved), Is.LessThanOrEqualTo(2560));
    }
}
