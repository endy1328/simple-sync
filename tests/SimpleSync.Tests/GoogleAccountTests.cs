using System.Reflection;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SimpleSync.Tests;

public static class GoogleAccountTests
{
    public static async Task RunAsync()
    {
        var serviceType = typeof(SyncPair).Assembly.GetType("SimpleSync.GoogleAccountService");
        Check(serviceType is not null, "Google account service must be available");
        var root = Path.Combine(AppContext.BaseDirectory, "auth-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (var unconfigured = new GoogleAccountService(Path.Combine(root, "unconfigured"), null, null))
                Check(!unconfigured.HasClientConfiguration, "source-only builds must report missing configuration honestly");
            using (var resource = typeof(GoogleAccountService).Assembly.GetManifestResourceStream("SimpleSync.GoogleOAuthClient.json"))
            using (var fresh = new GoogleAccountService(Path.Combine(root, "fresh-install")))
                Check(fresh.HasClientConfiguration == (resource is not null), "fresh installs must automatically load the embedded client");
            var bundle = "{\"installed\":{\"client_id\":\"bundled.apps.googleusercontent.com\",\"client_secret\":\"public-desktop-client\"}}";
            var bundledRoot = Path.Combine(root, "bundled");
            using (var bundled = new GoogleAccountService(bundledRoot, null, bundle))
                Check(bundled.HasClientConfiguration, "bundled desktop client must allow login without JSON import");
            using (var upgraded = new GoogleAccountService(bundledRoot, null, "invalid-json"))
                Check(upgraded.HasClientConfiguration, "saved client must retain precedence across bundled client upgrades");
            var bundledTokens = new[] { new { Id = "account-a", Email = "a@example.com", AccessToken = "expired", RefreshToken = "refresh-a", ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) } };
            File.WriteAllBytes(Path.Combine(bundledRoot, "accounts.dat"), GoogleCredentialProtection.Protect(JsonSerializer.SerializeToUtf8Bytes(bundledTokens)));
            var pinnedTransport = new TokenTransport();
            using (var http = new HttpClient(pinnedTransport))
            using (var upgraded = new GoogleAccountService(bundledRoot, http, bundle.Replace("bundled.apps", "replacement.apps")))
            using (await upgraded.CreateAuthenticatedClientAsync("account-a", CancellationToken.None))
                Check(pinnedTransport.Form.Contains("client_id=bundled.apps.googleusercontent.com", StringComparison.Ordinal),
                    "refresh tokens must stay bound to the client that originally granted access");
            ExpectFailure(() => new GoogleAccountService(Path.Combine(root, "invalid"), null, "{\"web\":{}}"),
                "invalid bundled client must be rejected");
            using var serviceOwner = (IDisposable)Activator.CreateInstance(serviceType!, new object?[] { root, null })!;
            dynamic service = serviceOwner;
            // Production builds may include the distributor's default desktop client.
            var configPath = Path.Combine(root, "desktop.json");
            File.WriteAllText(configPath, "{\"web\":{\"client_id\":\"wrong\"}}");
            ExpectFailure(() => service.ImportClientConfiguration(configPath), "web client must be rejected");
            File.WriteAllText(configPath, "{\"installed\":{\"client_id\":\"test.apps.googleusercontent.com\",\"client_secret\":\"desktop-secret\"}}");
            service.ImportClientConfiguration(configPath);
            Check((bool)service.HasClientConfiguration, "valid installed client must load");
            service.ImportClientConfiguration(configPath);
            using var reloadedOwner = (IDisposable)Activator.CreateInstance(serviceType!, new object?[] { root, null })!;
            dynamic reloaded = reloadedOwner;
            Check((bool)reloaded.HasClientConfiguration, "client config must survive restart");
            var saved = File.ReadAllBytes(Path.Combine(root, "client.dat"));
            Check(!Encoding.UTF8.GetString(saved).Contains("desktop-secret", StringComparison.Ordinal), "client config must be encrypted");
            Check(!((System.Collections.IEnumerable)reloaded.Accounts).Cast<object>().Any(), "fresh account list must be empty");
            try
            {
                using HttpClient unexpected = await reloaded.CreateAuthenticatedClientAsync("unknown", CancellationToken.None);
                throw new InvalidOperationException("missing account must require reconnect");
            }
            catch (InvalidOperationException ex) when (ex.Message != "missing account must require reconnect") { }

            var accountPath = Path.Combine(root, "accounts.dat");
            var fixture = new[]
            {
                new { Id = "account-a", Email = "a@example.com", AccessToken = "expired-a", RefreshToken = "refresh-a", ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) },
                new { Id = "account-b", Email = "b@example.com", AccessToken = "active-b", RefreshToken = "refresh-b", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) }
            };
            File.WriteAllBytes(accountPath, GoogleCredentialProtection.Protect(JsonSerializer.SerializeToUtf8Bytes(fixture)));
            var tokenTransport = new TokenTransport();
            using var tokenHttp = new HttpClient(tokenTransport);
            using (var connected = new GoogleAccountService(root, tokenHttp))
            {
                Check(connected.Accounts.Count == 2, "encrypted accounts must survive reload independently");
                using var client = await connected.CreateAuthenticatedClientAsync("account-a", CancellationToken.None);
                Check(tokenTransport.Calls == 1, "expired account must refresh before API usage");
                Check(tokenTransport.Form.Contains("refresh_token=refresh-a", StringComparison.Ordinal), "refresh must use only selected account credential");
                using var same = await connected.CreateAuthenticatedClientAsync("account-a", CancellationToken.None);
                Check(tokenTransport.Calls == 1, "unexpired refreshed token must be reused");
                using var other = await connected.CreateAuthenticatedClientAsync("account-b", CancellationToken.None);
                Check(tokenTransport.Calls == 1, "other active account must not refresh");
                File.WriteAllText(configPath, "{\"installed\":{\"client_id\":\"other.apps.googleusercontent.com\"}}");
                ExpectFailure(() => connected.ImportClientConfiguration(configPath), "changing client while accounts remain must be rejected");
                connected.RemoveAccount("account-a");
                Check(connected.Accounts.Single().Id == "account-b", "disconnect must preserve other account");
            }
            using (var remaining = new GoogleAccountService(root))
                Check(remaining.Accounts.Single().Id == "account-b", "account removal must persist");
            var protectedBytes = GoogleCredentialProtection.Protect(Encoding.UTF8.GetBytes("credential"));
            Check(Encoding.UTF8.GetString(GoogleCredentialProtection.Unprotect(protectedBytes)) == "credential", "DPAPI must roundtrip credentials for this Windows user");
            File.WriteAllBytes(Path.Combine(root, "accounts.dat"), [1, 2, 3, 4]);
            ExpectFailure(() => Activator.CreateInstance(serviceType!, new object?[] { root, null }), "corrupt credentials must not silently reset accounts");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void ExpectFailure(Action action, string message)
    {
        try { action(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TokenTransport : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Form { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Check(request.RequestUri?.AbsoluteUri == "https://oauth2.googleapis.com/token", "test must never contact an external API");
            Calls++;
            Form = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"refreshed-a\",\"expires_in\":3600}") };
        }
    }
}
