using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimpleSync;

public sealed record GoogleAccount(string Id, string Email);

/// <summary>Desktop OAuth credentials stay in this Windows user's encrypted data store.</summary>
public sealed class GoogleAccountService : IDisposable
{
    private const string Scope = "openid email https://www.googleapis.com/auth/drive";
    private readonly string _directory;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Dictionary<string, AccountToken> _tokens = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _gate = new();
    private ClientConfiguration? _client;

    public GoogleAccountService(string? dataDirectory = null, HttpClient? httpClient = null)
    {
        _directory = dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleSync", IsDebug ? "GoogleDrive.Debug" : "GoogleDrive");
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        _ownsHttp = httpClient is null;
        Directory.CreateDirectory(_directory);
        _client = ReadEncrypted<ClientConfiguration>("client.dat");
        foreach (var token in ReadEncrypted<List<AccountToken>>("accounts.dat") ?? [])
        {
            if (string.IsNullOrWhiteSpace(token.Id) || string.IsNullOrWhiteSpace(token.RefreshToken))
                throw new InvalidOperationException("저장된 Google 계정 정보가 올바르지 않습니다. / Invalid saved Google credentials.");
            _tokens.Add(token.Id, token);
        }
    }

    private static bool IsDebug
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public IReadOnlyList<GoogleAccount> Accounts
    {
        get { lock (_gate) return _tokens.Values.Select(t => new GoogleAccount(t.Id, t.Email)).OrderBy(t => t.Email).ToList(); }
    }

    public bool HasClientConfiguration => _client is not null;

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
        _refreshLock.Dispose();
    }

    public void ImportClientConfiguration(string jsonPath)
    {
        ClientConfiguration client;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var installed = document.RootElement.GetProperty("installed");
            var id = installed.GetProperty("client_id").GetString();
            if (string.IsNullOrWhiteSpace(id) || !id.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal))
                throw new JsonException();
            client = new ClientConfiguration(id, installed.TryGetProperty("client_secret", out var secret) ? secret.GetString() ?? "" : "");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException("Google Cloud에서 다운로드한 Desktop app 클라이언트 JSON을 선택하세요. / Select a Desktop app client JSON.", ex);
        }
        lock (_gate)
        {
            if (_tokens.Count > 0 && _client?.ClientId != client.ClientId)
                throw new InvalidOperationException("OAuth 클라이언트를 변경하려면 먼저 연결된 계정을 해제하세요. / Disconnect accounts before changing the OAuth client.");
            SaveEncrypted("client.dat", client);
            _client = client;
        }
    }

    public async Task<GoogleAccount> ConnectAsync(CancellationToken cancellationToken)
    {
        var client = _client ?? throw new InvalidOperationException("먼저 Desktop OAuth 클라이언트 JSON을 가져오세요. / Import Desktop OAuth client JSON first.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        var ct = deadline.Token;
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var redirect = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/oauth2callback";
            var url = "https://accounts.google.com/o/oauth2/v2/auth?" + FormQuery(new Dictionary<string, string>
            {
                ["client_id"] = client.ClientId, ["redirect_uri"] = redirect, ["response_type"] = "code", ["scope"] = Scope,
                ["code_challenge"] = challenge, ["code_challenge_method"] = "S256", ["state"] = state,
                ["access_type"] = "offline", ["prompt"] = "consent select_account"
            });
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            var code = await ReceiveCodeAsync(listener, state, ct).ConfigureAwait(false);
            using var tokenJson = await RequestTokenAsync(new Dictionary<string, string>
            {
                ["client_id"] = client.ClientId, ["client_secret"] = client.ClientSecret,
                ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code", ["code"] = code, ["code_verifier"] = verifier
            }, ct).ConfigureAwait(false);
            var json = tokenJson.RootElement;
            var access = json.GetProperty("access_token").GetString()!;
            if (!json.TryGetProperty("refresh_token", out var refresh) || string.IsNullOrWhiteSpace(refresh.GetString()))
                throw new InvalidOperationException("오프라인 접근 승인이 필요합니다. 계정을 다시 연결하세요. / Reconnect to grant offline access.");
            if (json.TryGetProperty("scope", out var granted) && !granted.GetString()!.Split(' ').Contains("https://www.googleapis.com/auth/drive"))
                throw new InvalidOperationException("Drive 접근 권한이 승인되지 않았습니다. / Drive access was not granted.");
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Google 계정 정보를 확인하지 못했습니다. / Could not verify the Google account.");
            using var identity = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var user = identity.RootElement;
            if (user.TryGetProperty("hd", out _))
                throw new InvalidOperationException("현재는 개인 Google 계정만 지원합니다. / Only personal Google accounts are supported.");
            var id = user.GetProperty("sub").GetString();
            var email = user.GetProperty("email").GetString();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(email)
                || !user.TryGetProperty("email_verified", out var verified) || !verified.GetBoolean())
                throw new InvalidOperationException("검증된 Google 계정을 확인하지 못했습니다. / A verified Google account is required.");
            var token = new AccountToken(id, email, access, refresh.GetString()!, Expiry(json));
            lock (_gate)
            {
                if (_client?.ClientId != client.ClientId)
                    throw new InvalidOperationException("로그인 중 OAuth 클라이언트가 변경되었습니다. 다시 연결하세요. / OAuth client changed during sign-in; reconnect.");
                var updated = _tokens.Values.Where(t => t.Id != id).Append(token).ToList();
                SaveEncrypted("accounts.dat", updated);
                _tokens[id] = token;
            }
            return new GoogleAccount(id, email);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Google 로그인 시간이 초과되었습니다. 다시 연결하세요. / Google sign-in timed out; try again.");
        }
        finally { listener.Stop(); }
    }

    public void RemoveAccount(string id)
    {
        lock (_gate)
        {
            if (!_tokens.ContainsKey(id)) return;
            SaveEncrypted("accounts.dat", _tokens.Values.Where(t => t.Id != id).ToList());
            _tokens.Remove(id);
        }
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string accountId, CancellationToken cancellationToken)
    {
        await GetAccessTokenAsync(accountId, forceRefresh: false, cancellationToken).ConfigureAwait(false);
        return new HttpClient(new AccountHttpHandler(this, accountId)) { Timeout = TimeSpan.FromMinutes(30) };
    }

    private async Task<string> GetAccessTokenAsync(string accountId, bool forceRefresh, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            AccountToken token;
            lock (_gate)
            {
                if (!_tokens.TryGetValue(accountId, out token!))
                    throw new InvalidOperationException("Google 계정 연결이 필요합니다. / Google account connection required.");
            }
            if (!forceRefresh && token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) return token.AccessToken;
            var client = _client ?? throw new InvalidOperationException("OAuth 클라이언트 설정이 필요합니다. / OAuth client configuration required.");
            using var document = await RequestTokenAsync(new Dictionary<string, string>
            {
                ["client_id"] = client.ClientId, ["client_secret"] = client.ClientSecret,
                ["grant_type"] = "refresh_token", ["refresh_token"] = token.RefreshToken
            }, ct).ConfigureAwait(false);
            var json = document.RootElement;
            var updated = token with
            {
                AccessToken = json.GetProperty("access_token").GetString()!, ExpiresAt = Expiry(json),
                RefreshToken = json.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString()! : token.RefreshToken
            };
            lock (_gate)
            {
                // Disconnect during a pending refresh must not resurrect the account.
                if (!_tokens.ContainsKey(accountId)) throw new InvalidOperationException("Google 계정 연결이 해제되었습니다. / Google account disconnected.");
                SaveEncrypted("accounts.dat", _tokens.Values.Where(t => t.Id != accountId).Append(updated).ToList());
                _tokens[accountId] = updated;
            }
            return updated.AccessToken;
        }
        finally { _refreshLock.Release(); }
    }

    private async Task<JsonDocument> RequestTokenAsync(Dictionary<string, string> parameters, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(parameters.Where(p => p.Value.Length > 0));
        using var response = await _http.PostAsync("https://oauth2.googleapis.com/token", content, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Google 인증에 실패했습니다. 계정을 다시 연결하세요. / Google authentication failed; reconnect the account.");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    private static DateTimeOffset Expiry(JsonElement json) => DateTimeOffset.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32());
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string FormQuery(Dictionary<string, string> values) => string.Join("&", values.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));

    private static async Task<string> ReceiveCodeAsync(TcpListener listener, string expectedState, CancellationToken ct)
    {
        while (true)
        {
            using var socket = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            using var stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            requestDeadline.CancelAfter(TimeSpan.FromSeconds(10));
            var line = await reader.ReadLineAsync(requestDeadline.Token).ConfigureAwait(false);
            var parts = line?.Split(' ');
            if (parts is not { Length: >= 2 } || parts[0] != "GET" || parts[1].Length > 8192
                || !Uri.TryCreate("http://127.0.0.1" + parts[1], UriKind.Absolute, out var uri))
            {
                await RespondAsync(stream, false, ct).ConfigureAwait(false);
                continue;
            }
            var query = ParseQuery(uri.Query);
            if (uri.AbsolutePath != "/oauth2callback" || !query.TryGetValue("state", out var state)
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(expectedState)))
            {
                await RespondAsync(stream, false, ct).ConfigureAwait(false);
                continue;
            }
            var success = query.TryGetValue("code", out var code) && !string.IsNullOrWhiteSpace(code) && !query.ContainsKey("error");
            await RespondAsync(stream, success, ct).ConfigureAwait(false);
            return success ? code! : throw new InvalidOperationException("Google 로그인이 취소되었거나 권한이 거부되었습니다. / Google sign-in cancelled or access denied.");
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
            if (!result.TryAdd(key, parts.Length == 2 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : ""))
                throw new InvalidOperationException("잘못된 OAuth 응답입니다. / Invalid OAuth response.");
        }
        return result;
    }

    private static async Task RespondAsync(Stream stream, bool success, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes(success
            ? "<!doctype html><meta charset=utf-8><title>simple sync</title><p>Google sign-in received. Close this tab and return to simple sync.</p>"
            : "<!doctype html><meta charset=utf-8><title>simple sync</title><p>Invalid sign-in response. Return to simple sync.</p>");
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(success ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(body, ct).ConfigureAwait(false);
    }

    private T? ReadEncrypted<T>(string name)
    {
        var path = Path.Combine(_directory, name);
        if (!File.Exists(path)) return default;
        try
        {
            var bytes = GoogleCredentialProtection.Unprotect(File.ReadAllBytes(path));
            try { return JsonSerializer.Deserialize<T>(bytes) ?? throw new JsonException(); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException("저장된 Google 인증 정보를 읽지 못했습니다. 다른 Windows 사용자의 파일이거나 손상되었습니다. / Saved Google credentials are unreadable or belong to another Windows user.", ex);
        }
    }

    private void SaveEncrypted<T>(string name, T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        byte[] encrypted;
        try { encrypted = GoogleCredentialProtection.Protect(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        var path = Path.Combine(_directory, name);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(encrypted);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temp); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private sealed record ClientConfiguration(string ClientId, string ClientSecret);
    private sealed record AccountToken(string Id, string Email, string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

    private sealed class AccountHttpHandler(GoogleAccountService owner, string accountId) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ValidateDestination(request.RequestUri);
            var originalToken = await owner.GetAccessTokenAsync(accountId, false, ct).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", originalToken);
            var response = await base.SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                await owner.GetAccessTokenAsync(accountId, true, ct).ConfigureAwait(false);
                // Reads and bodyless operations can be safely retried once. Streaming writes
                // are retried by the endpoint/session layer, never replayed blindly here.
                if (request.Content is null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await owner.GetAccessTokenAsync(accountId, false, ct).ConfigureAwait(false));
                    return await base.SendAsync(request, ct).ConfigureAwait(false);
                }
                throw new InvalidOperationException("Google 인증이 갱신되었습니다. 동기화를 다시 실행하세요. / Google authorization refreshed; run sync again.");
            }
            return response;
        }

        private static void ValidateDestination(Uri? uri)
        {
            if (uri is null || uri.Scheme != Uri.UriSchemeHttps || !(uri.Host == "www.googleapis.com" || uri.Host.EndsWith(".googleapis.com", StringComparison.Ordinal)))
                throw new InvalidOperationException("Google API 이외의 주소로 인증 정보를 전송할 수 없습니다. / Authentication is restricted to Google API endpoints.");
        }
    }
}

public static class GoogleCredentialProtection
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    public static byte[] Protect(byte[] input) => Transform(input, protect: true);
    public static byte[] Unprotect(byte[] input) => Transform(input, protect: false);

    private static byte[] Transform(byte[] input, bool protect)
    {
        var blob = new DataBlob { Size = input.Length, Data = Marshal.AllocHGlobal(input.Length) };
        DataBlob result = default;
        try
        {
            Marshal.Copy(input, 0, blob.Data, input.Length);
            var ok = protect ? CryptProtectData(ref blob, "simple sync Google credentials", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out result)
                : CryptUnprotectData(ref blob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out result);
            if (!ok) throw new CryptographicException(Marshal.GetLastWin32Error());
            var output = new byte[result.Size];
            Marshal.Copy(result.Data, output, 0, result.Size);
            return output;
        }
        finally
        {
            if (blob.Data != IntPtr.Zero)
            {
                for (var index = 0; index < blob.Size; index++) Marshal.WriteByte(blob.Data, index, 0);
                Marshal.FreeHGlobal(blob.Data);
            }
            if (result.Data != IntPtr.Zero)
            {
                for (var index = 0; index < result.Size; index++) Marshal.WriteByte(result.Data, index, 0);
                LocalFree(result.Data);
            }
        }
    }
}
