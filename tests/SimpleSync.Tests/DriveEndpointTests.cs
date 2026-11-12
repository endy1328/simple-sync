using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SimpleSync;

public static class DriveEndpointTests
{
    private static readonly CancellationToken Token = CancellationToken.None;
    private static SyncFilter Filter(SyncPair? pair = null) => SyncFilter.FromPair(pair ?? new SyncPair());
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task Throws(Func<Task> action, string message)
    {
        try { await action(); } catch (IOException) { return; }
        throw new InvalidOperationException(message);
    }

    public static async Task BenchmarkAsync()
    {
        using var server = new FakeDrive();
        var endpoint = new GoogleDriveEndpoint(server.Client, "root");
        await endpoint.ListAsync(Filter(), Token);
        for (var i = 0; i < 454; i++) await endpoint.EnsureDirectoryAsync("folder" + i, Token);
        for (var i = 0; i < 2070; i++) await endpoint.WriteAsync($"folder{i % 454}/note{i}.md", null, new MemoryStream([1, 2, 3]), Token);
        Console.WriteLine("INITIAL " + server.Counts());
        server.ResetCounts();
        await endpoint.ListAsync(Filter(), Token);
        for (var i = 0; i < 454; i++) await endpoint.EnsureDirectoryAsync("folder" + i, Token);
        Console.WriteLine("UNCHANGED " + server.Counts());
    }

    public static async Task RunAsync()
    {
        using (var server = new FakeDrive())
        {
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            await endpoint.ListAsync(Filter(), Token);
            for (var i = 0; i < 10; i++) await endpoint.WriteAsync($"folder/note{i}.md", null, new MemoryStream([1,2,3]), Token);
            Assert(server.MultipartUploads == 10, "small files must use one multipart request");
            Assert(server.IdRequests == 1, "reserved IDs must be batched");
            Assert(server.MetadataRequests <= 24, "writes must retain fresh ancestry checks without repeated preparation checks");
            Assert(server.ChildRequests <= 12, "directory preparation must avoid repeated listings while writes freshly check conflicts");
        }
        foreach (var directory in new[] { false, true })
        {
            using var server = new FakeDrive();
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            await endpoint.ListAsync(Filter(), Token);
            await endpoint.WriteAsync("warm.md", null, new MemoryStream([1]), Token);
            server.Add("external", "appeared", "text/plain");
            if (directory)
                await Throws(() => endpoint.EnsureDirectoryAsync("appeared", Token), "warmed child cache must detect a binary directory conflict");
            else
                await Throws(() => endpoint.WriteAsync("appeared", null, new MemoryStream([2]), Token), "warmed child cache must detect an unplanned target");
            Assert(server.Files.Values.Count(file => file.Name == "appeared") == 1 && server.GeneratedIds == 1, "conflict must survive unchanged without duplicate creation");
        }
        foreach (var lost in new[] { false, true })
        {
            using var server = new FakeDrive { LoseFirstUploadResponse = lost, OmitUploadMetadata = !lost };
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            await endpoint.ListAsync(Filter(), Token);
            using var source = new MemoryStream([9, 8, 7]);
            await endpoint.WriteAsync("small.md", null, source, Token);
            Assert(server.Files.Count == 1 && server.Files.Values.Single().Content.SequenceEqual(new byte[] {9,8,7}), "multipart recovery must preserve one complete file");
            Assert(server.MetadataRequests == 3, "multipart response loss or missing metadata requires fresh verification GET");
        }
        using (var server = new FakeDrive { CorruptUploadResponse = true })
        {
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            await endpoint.ListAsync(Filter(), Token);
            await Throws(() => endpoint.WriteAsync("bad.md", null, new MemoryStream([1,2]), Token), "checksum mismatch must fail the transfer");
        }
        using (var server = new FakeDrive())
        {
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            await endpoint.ListAsync(Filter(), Token);
            await Task.WhenAll(Enumerable.Range(0, 24).Select(async i =>
            {
                using var source = new MemoryStream([1,2,3]);
                await endpoint.WriteAsync($"same/child{i}.md", null, source, Token);
            }));
            Assert(server.Files.Values.Count(f => f.Mime == GoogleDriveEndpoint.FolderMimeType) == 1 && server.Files.Count == 25,
                "concurrent writes must create one parent and distinct reserved file IDs");
            Assert(server.IdRequests == 1 && server.ChildRequests == 26, "concurrent writes must share one reservation batch and freshly check names");
            await endpoint.ListAsync(Filter(), Token);
            server.Add("new", "unexpected.md", parent: server.Files.Values.Single(f => f.Mime == GoogleDriveEndpoint.FolderMimeType).Id);
            await Throws(() => endpoint.WriteAsync("same/unexpected.md", null, new MemoryStream([3]), Token), "new run must refresh the write listing and reject unplanned files");
        }
        foreach (var mutation in new[] { "parent", "ancestor", "root" })
        {
            using var server = new FakeDrive();
            server.Add("a", "a", GoogleDriveEndpoint.FolderMimeType);
            server.Add("b", "b", GoogleDriveEndpoint.FolderMimeType, parent: "a");
            server.Add("file", "file.md", parent: "b");
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            var entries = await endpoint.ListAsync(Filter(), Token);
            await endpoint.EnsureDirectoryAsync("a/b", Token);
            if (mutation == "root") server.RootUnavailable = true;
            else server.Files[mutation == "parent" ? "b" : "a"].Parent = "elsewhere";
            await Throws(() => endpoint.WriteAsync("a/b/new.md", null, new MemoryStream([1]), Token), "cached directory must not bypass fresh write ancestry: " + mutation);
            await Throws(() => endpoint.DeleteAsync(entries.Single(f => f.Id == "file"), Token), "fresh deletion ancestry must reject moved folders: " + mutation);
            Assert(server.MultipartUploads == 0 && server.Deleted.Count == 0, "unsafe ancestry must fail before mutation");
        }
        using (var server = new FakeDrive { LoseFirstFolderResponse = true })
        {
            server.Add("parent", "parent", GoogleDriveEndpoint.FolderMimeType);
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            var created = await endpoint.CreateFolderAsync("parent", "새 폴더", Token);
            Assert(created.IsDirectory && created.RelativePath == "새 폴더" && server.Files[created.Id].Parent == "parent", "folder must be created in the selected parent");
            Assert(server.GeneratedIds == 1 && server.Files.Values.Count(file => file.Name == "새 폴더") == 1, "lost creation response must not create duplicate folders");
            await Throws(() => endpoint.CreateFolderAsync("parent", "새 폴더", Token), "duplicate folder must be rejected");
            await Throws(() => endpoint.CreateFolderAsync("parent", "../escape", Token), "invalid folder name must be rejected");
            server.Add("outside", "outside", GoogleDriveEndpoint.FolderMimeType, parent: "unrelated");
            await Throws(() => endpoint.CreateFolderAsync("outside", "child", Token), "parent outside selected Drive root must be rejected");
            Assert(server.GeneratedIds == 1, "validation failure must not reserve IDs or create folders");
        }
        foreach (var detail in new[] { "SERVICE_DISABLED", "ACCESS_TOKEN_SCOPE_INSUFFICIENT", "insufficientPermissions", "domainPolicy" })
        {
            using var server = new FakeDrive { ForbiddenReason = detail };
            try
            {
                await new GoogleDriveEndpoint(server.Client, "root").ListChildrenAsync("root", Token);
                throw new InvalidOperationException("403 must fail");
            }
            catch (IOException error)
            {
                Assert(error.Message.Contains(detail), "403 must expose its safe specific reason even after retry classification");
                Assert(!error.Message.Contains("secret-token") && !error.Message.Contains("secret-session"), "raw API messages must not leak credentials or URLs");
                if (detail == "SERVICE_DISABLED") Assert(error.Message.Contains("123456789"), "disabled service must identify the affected project number");
            }
        }
        using (var server = new FakeDrive { PaginateRoot = true })
        {
            server.Add("doc", "report", "application/vnd.google-apps.document");
            server.Add("sheet", "budget", "application/vnd.google-apps.spreadsheet");
            server.Add("slides", "pitch", "application/vnd.google-apps.presentation");
            server.Add("sub", "nested", GoogleDriveEndpoint.FolderMimeType);
            server.Add("txt", "note.txt", content: "hello", parent: "sub");
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            var entries = await endpoint.ListAsync(Filter(), Token);
            Assert(entries.Count == 5 && entries.Any(e => e.RelativePath == "nested/note.txt"), "complete traversal must include both pages and nested files");
            foreach (var pair in new[] { ("doc", "report.docx", "wordprocessingml"), ("sheet", "budget.xlsx", "spreadsheetml"), ("slides", "pitch.pptx", "presentationml") })
            {
                var entry = entries.Single(e => e.Id == pair.Item1);
                Assert(entry.RelativePath == pair.Item2, "native document must use its Office extension");
                using var downloaded = new MemoryStream();
                await endpoint.ReadAsync(entry, downloaded, Token);
                Assert(Encoding.UTF8.GetString(downloaded.ToArray()) == "exported:" + pair.Item1, "export must return content");
                Assert(server.Urls.Any(url => url.Contains("/" + pair.Item1 + "/export") && Uri.UnescapeDataString(url).Contains(pair.Item3)), "correct Office export MIME must be requested");
            }
        }
        foreach (var mode in new[] { "incomplete", "malformed", "repeated" })
        {
            using var server = new FakeDrive { BadListing = mode };
            await Throws(async () => await new GoogleDriveEndpoint(server.Client, "root").ListAsync(Filter(), Token), "unsafe listing must fail: " + mode);
            Assert(server.Deleted.Count == 0, "listing errors must not mutate Drive");
        }
        using (var server = new FakeDrive())
        {
            server.Add("shortcut", "link", "application/vnd.google-apps.shortcut");
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            await Throws(async () => await endpoint.ListAsync(Filter(), Token), "relevant shortcuts must fail safely");
            Assert((await endpoint.ListAsync(Filter(new SyncPair { ExcludePatterns = ["link"] }), Token)).Count == 0, "excluded shortcuts must not stop a filtered job");
        }
        using (var server = new FakeDrive())
        {
            server.Add("f", "file.txt", content: "hello");
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            var entry = (await endpoint.ListAsync(Filter(), Token)).Single();
            server.Files["f"].Parent = "elsewhere";
            await Throws(() => endpoint.ReadAsync(entry, new MemoryStream(), Token), "moved source must not be read using stale identity");
            await Throws(() => endpoint.DeleteAsync(entry, Token), "moved target must not be deleted");
            Assert(server.Deleted.Count == 0, "out-of-root item must survive");
        }
        using (var server = new FakeDrive())
        {
            server.Add("owned", "owned.txt"); server.Add("shared", "shared.txt"); server.Files["shared"].Owned = false;
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            var entries = await endpoint.ListAsync(Filter(), Token);
            await Throws(() => endpoint.DeleteAsync(entries.Single(e => e.Id == "shared"), Token), "non-owned file must not be permanently deleted");
            await endpoint.DeleteAsync(entries.Single(e => e.Id == "owned"), Token);
            Assert(server.Deleted.SequenceEqual(["owned"]) && server.PatchCount == 0, "permanent DELETE must remove only owned file and never trash PATCH");
        }
        using (var server = new FakeDrive())
        {
            server.Add("folder", "folder", GoogleDriveEndpoint.FolderMimeType); server.Add("protected", "excluded.txt", parent: "folder");
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            var entries = await endpoint.ListAsync(Filter(new SyncPair { ExcludePatterns = ["excluded.txt"] }), Token);
            await endpoint.DeleteAsync(entries.Single(e => e.Id == "folder"), Token);
            Assert(server.Deleted.Count == 0, "excluded descendants must survive remote folder cleanup");
        }
        foreach (var lost in new[] { false, true })
        {
            using var server = new FakeDrive { FailFirstInitiation = !lost, LoseFirstUploadResponse = lost };
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            await endpoint.ListAsync(Filter(), Token);
            var officeBytes = new byte[1024 * 1024 + 1]; Encoding.UTF8.GetBytes("original office bytes").CopyTo(officeBytes, 0);
            using var source = new MemoryStream(officeBytes);
            await endpoint.WriteAsync("report.docx", null, source, Token);
            var uploaded = server.Files.Values.Single(f => f.Name == "report.docx");
            Assert(uploaded.Mime == "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Office upload must retain original format");
            Assert(uploaded.Content.SequenceEqual(officeBytes), "upload original bytes");
            Assert(server.GeneratedIds == 1 && server.InitiationIds.Distinct().Count() == 1, "retry must reuse reserved file ID");
            if (lost) Assert(server.StatusQueries == 1, "lost response must query session before retry");
        }
        using (var server = new FakeDrive { RateLimitOnce = true })
        {
            server.Add("f", "file.txt");
            Assert((await new GoogleDriveEndpoint(server.Client, "root").ListAsync(Filter(), Token)).Count == 1, "Drive 403 rateLimitExceeded must be retried");
        }
        using (var server = new FakeDrive())
        {
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            await endpoint.ListAsync(Filter(), Token);
            var bytes = new byte[8 * 1024 * 1024 + 17]; bytes[^1] = 7;
            await endpoint.WriteAsync("chunks.bin", null, new MemoryStream(bytes), Token);
            Assert(server.UploadChunks == 2 && server.Files.Values.Single().Content.SequenceEqual(bytes), "308 chunk continuation must preserve full file content");
        }
        using (var server = new FakeDrive())
        {
            server.Add("existing", "Replace.TXT", content: "target old content");
            var endpoint = new GoogleDriveEndpoint(server.Client, "root", false);
            var existing = (await endpoint.ListAsync(Filter(), Token)).Single();
            await endpoint.WriteAsync("replace.txt", existing, new MemoryStream(Encoding.UTF8.GetBytes("source wins")), Token);
            Assert(Encoding.UTF8.GetString(server.Files["existing"].Content) == "source wins" && server.Files["existing"].Name == "replace.txt" && server.GeneratedIds == 0 && server.PatchCount == 1, "existing binary file must be replaced by ID including source filename casing without duplicate creation");
            await endpoint.ListAsync(Filter(), Token);
            await endpoint.WriteAsync("empty.bin", null, new MemoryStream(), Token);
            Assert(server.Files.Values.Single(file => file.Name == "empty.bin").Content.Length == 0, "zero-byte files must upload successfully");
        }
        using (var server = new FakeDrive())
        {
            server.Add("native", "report", "application/vnd.google-apps.document");
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            var native = (await endpoint.ListAsync(Filter(), Token)).Single();
            await Throws(() => endpoint.WriteAsync(native.RelativePath, native, new MemoryStream([1, 2]), Token), "native target must not be changed into binary in place");
            Assert(server.GeneratedIds == 0 && server.PatchCount == 0, "native conflict must not mutate anything");
            var constructor = typeof(GoogleDriveEndpoint).GetConstructor([typeof(HttpClient), typeof(string), typeof(bool)]);
            Assert(constructor is not null, "Drive endpoint needs native export view option for source versus target");
            var target = (GoogleDriveEndpoint)constructor!.Invoke([server.Client, "root", false]);
            Assert((await target.ListAsync(Filter(), Token)).Single().RelativePath == "report", "upload target must use actual native filename");
        }
        using (var server = new FakeDrive())
        {
            server.Add("dir", "parent", GoogleDriveEndpoint.FolderMimeType); server.Add("child", "child", GoogleDriveEndpoint.FolderMimeType, parent: "dir"); server.Add("txt", "ignore.txt", parent: "dir");
            var endpoint = new GoogleDriveEndpoint(server.Client, "root");
            var folders = await endpoint.ListChildrenAsync("dir", Token);
            Assert(folders.Count == 1 && folders[0].RelativePath == "child" && folders[0].Id == "child", "picker needs direct child folders with raw name and ID");
            await endpoint.ListAsync(Filter(), Token);
            server.Add("new", "same.txt", content: "external edit");
            await Throws(() => endpoint.WriteAsync("same.txt", null, new MemoryStream([1]), Token), "unplanned target must not be overwritten");
            Assert(server.GeneratedIds == 0, "unplanned conflict must be detected before upload");
        }
    }

    internal sealed class Remote(string id, string name, string mime, string parent, byte[] content)
    {
        public string Id = id, Name = name, Mime = mime, Parent = parent;
        public byte[] Content = content;
        public bool Owned = true;
        public object Json() => new { id = Id, name = Name, mimeType = Mime, parents = new[] { Parent }, trashed = false, ownedByMe = Owned,
            size = Content.Length.ToString(), md5Checksum = Convert.ToHexString(MD5.HashData(Content)), version = "1", modifiedTime = "2026-11-11T00:00:00Z",
            capabilities = new { canEdit = true, canDelete = true, canListChildren = true } };
    }

    internal sealed class FakeDrive : HttpMessageHandler
    {
        public string? ForbiddenReason;
        public HttpClient Client { get; }
        public Dictionary<string, Remote> Files = new(StringComparer.Ordinal);
        public List<string> Urls = [], Deleted = [], InitiationIds = [];
        public string? BadListing;
        public bool PaginateRoot, FailFirstInitiation, LoseFirstUploadResponse, RateLimitOnce;
        public bool LoseFirstFolderResponse, OmitUploadMetadata, CorruptUploadResponse, RootUnavailable;
        public bool BlockFolderCreation, FolderCreationCancelled;
        public volatile bool FolderCreationStarted;
        public int GeneratedIds, PatchCount, StatusQueries, UploadChunks;
        private int _nextId;
        private readonly SemaphoreSlim _requestGate = new(1, 1);
        public int MetadataRequests, ChildRequests, IdRequests, FolderRequests, MultipartUploads, SessionRequests;
        public string Counts() => $"metadata={MetadataRequests}, children={ChildRequests}, ids={IdRequests}, folders={FolderRequests}, multipart={MultipartUploads}, sessions={SessionRequests}, chunks={UploadChunks}, total={Urls.Count}";
        public void ResetCounts() { MetadataRequests = ChildRequests = IdRequests = FolderRequests = MultipartUploads = SessionRequests = UploadChunks = 0; Urls.Clear(); }
        private Remote? _upload;
        private bool _lostResponse;
        public FakeDrive() => Client = new HttpClient(this, disposeHandler: false);
        public void Add(string id, string name, string mime = "text/plain", string content = "", string parent = "actual-root") => Files[id] = new(id, name, mime, parent, Encoding.UTF8.GetBytes(content));
        protected override void Dispose(bool disposing) { if (disposing) Client.Dispose(); base.Dispose(disposing); }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            await _requestGate.WaitAsync(cancellationToken);
            try { return await HandleAsync(request, cancellationToken); }
            finally { _requestGate.Release(); }
        }
        private async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ForbiddenReason is not null)
            {
                var denied = Json(new { error = new { message = "secret-token https://example.com/secret-session",
                    errors = new[] { new { reason = ForbiddenReason } }, details = new[] {
                        new { reason = ForbiddenReason, metadata = new { consumer = "projects/123456789", service = "drive.googleapis.com" } } } } });
                denied.StatusCode = HttpStatusCode.Forbidden;
                return denied;
            }
            var uri = request.RequestUri!; Urls.Add(uri.AbsoluteUri);
            var path = uri.AbsolutePath; var query = Uri.UnescapeDataString(uri.Query);
            if (path.EndsWith("/generateIds"))
            {
                IdRequests++; GeneratedIds++;
                var count = int.Parse(Regex.Match(query, @"count=(\d+)").Groups[1].Value);
                var ids = Enumerable.Range(0, count).Select(_ => "reserved-" + ++_nextId).ToArray();
                return Json(new { ids });
            }
            if (path == "/drive/v3/files" && request.Method == HttpMethod.Post)
            {
                FolderRequests++;
                if (BlockFolderCreation)
                {
                    FolderCreationStarted = true;
                    try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                    catch (OperationCanceledException) { FolderCreationCancelled = true; throw; }
                }
                using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var body = payload.RootElement;
                var id = body.GetProperty("id").GetString()!;
                if (Files.ContainsKey(id)) return new(HttpStatusCode.Conflict);
                Add(id, body.GetProperty("name").GetString()!, body.GetProperty("mimeType").GetString()!, parent: body.GetProperty("parents")[0].GetString()!);
                if (LoseFirstFolderResponse) { LoseFirstFolderResponse = false; throw new HttpRequestException("simulated lost folder response"); }
                return Json(Files[id].Json());
            }
            if (path == "/session")
            {
                var range = request.Content!.Headers.GetValues("Content-Range").Single();
                if (range.StartsWith("bytes */", StringComparison.Ordinal)) { StatusQueries++; if (range == "bytes */0") Files[_upload!.Id] = _upload; return Json(_upload!.Json()); }
                var rangeParts = Regex.Match(range, @"bytes (\d+)-(\d+)/(\d+)");
                var start = int.Parse(rangeParts.Groups[1].Value); var end = int.Parse(rangeParts.Groups[2].Value); var total = int.Parse(rangeParts.Groups[3].Value);
                var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                if (_upload!.Content.Length != total) _upload.Content = new byte[total];
                bytes.CopyTo(_upload.Content, start); UploadChunks++;
                if (end + 1 < total) { var partial = new HttpResponseMessage((HttpStatusCode)308); partial.Headers.TryAddWithoutValidation("Range", "bytes=0-" + end); return partial; }
                Files[_upload.Id] = _upload;
                if (LoseFirstUploadResponse && !_lostResponse) { _lostResponse = true; throw new HttpRequestException("simulated response loss"); }
                return Json(_upload.Json());
            }
            if (path.Contains("/upload/") && query.Contains("uploadType=multipart"))
            {
                MultipartUploads++;
                var multipartParts = ((MultipartContent)request.Content!).ToArray();
                using var payload = JsonDocument.Parse(await multipartParts[0].ReadAsStringAsync(cancellationToken));
                var body = payload.RootElement;
                var bytes = await multipartParts[1].ReadAsByteArrayAsync(cancellationToken);
                Remote upload;
                if (request.Method == HttpMethod.Patch)
                {
                    PatchCount++; upload = Files[path.Split('/').Last()];
                    upload.Name = body.GetProperty("name").GetString()!;
                }
                else
                {
                    var id = body.GetProperty("id").GetString()!;
                    InitiationIds.Add(id);
                    if (Files.ContainsKey(id)) return new(HttpStatusCode.Conflict);
                    upload = new(id, body.GetProperty("name").GetString()!, body.GetProperty("mimeType").GetString()!, body.GetProperty("parents")[0].GetString()!, []);
                }
                upload.Content = bytes; Files[upload.Id] = upload;
                if (LoseFirstUploadResponse && !_lostResponse) { _lostResponse = true; throw new HttpRequestException("simulated multipart response loss"); }
                if (OmitUploadMetadata) return Json(new { });
                if (CorruptUploadResponse) return Json(new Remote(upload.Id, upload.Name, upload.Mime, upload.Parent, [4,5]).Json());
                return Json(upload.Json());
            }
            if (path.Contains("/upload/") && query.Contains("uploadType=resumable"))
            {
                SessionRequests++;
                using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)); var body = payload.RootElement;
                if (request.Method == HttpMethod.Patch)
                {
                    PatchCount++; _upload = Files[path.Split('/').Last()]; if (body.TryGetProperty("name", out var updateName)) _upload.Name = updateName.GetString()!;
                    var update = Json(new { }); update.Headers.Location = new Uri("https://www.googleapis.com/session"); return update;
                }
                var id = body.GetProperty("id").GetString()!; InitiationIds.Add(id);
                if (FailFirstInitiation && InitiationIds.Count == 1) return new(HttpStatusCode.ServiceUnavailable);
                _upload = new(id, body.GetProperty("name").GetString()!, body.GetProperty("mimeType").GetString()!, body.GetProperty("parents")[0].GetString()!, []);
                var response = Json(new { }); response.Headers.Location = new Uri("https://www.googleapis.com/session"); return response;
            }
            if (request.Method == HttpMethod.Patch) { PatchCount++; return Json(new { }); }
            if (request.Method == HttpMethod.Delete) { var id = path.Split('/').Last(); Deleted.Add(id); Files.Remove(id); return new(HttpStatusCode.NoContent); }
            if (path.EndsWith("/files") && request.Method == HttpMethod.Get)
            {
                ChildRequests++;
                if (RateLimitOnce) { RateLimitOnce = false; var limited = Json(new { error = new { errors = new[] { new { reason = "rateLimitExceeded" } } } }); limited.StatusCode = HttpStatusCode.Forbidden; return limited; }
                if (BadListing == "malformed") return Json(new { unexpected = true });
                if (BadListing == "incomplete") return Json(new { incompleteSearch = true, files = Array.Empty<object>() });
                if (BadListing == "repeated") return Json(new { nextPageToken = "same", files = Array.Empty<object>() });
                var parent = Regex.Match(query, "'([^']+)' in parents").Groups[1].Value;
                var children = Files.Values.Where(file => file.Parent == parent).ToList();
                if (PaginateRoot && parent == "actual-root")
                {
                    if (!query.Contains("pageToken=")) return Json(new { nextPageToken = "page2", files = children.Take(1).Select(file => file.Json()).ToArray() });
                    return Json(new { files = children.Skip(1).Select(file => file.Json()).ToArray() });
                }
                return Json(new { files = children.Select(file => file.Json()).ToArray() });
            }
            var parts = path.Split('/');
            if (parts.Last() == "export") return new(HttpStatusCode.OK) { Content = new StringContent("exported:" + parts[^2]) };
            MetadataRequests++;
            var fileId = parts.Last();
            if (RootUnavailable && (fileId is "root" or "actual-root")) return new(HttpStatusCode.NotFound);
            if (fileId is "root" or "actual-root") return Json(new Remote("actual-root", "My Drive", GoogleDriveEndpoint.FolderMimeType, "", []).Json());
            if (!Files.TryGetValue(fileId, out var remote)) return new(HttpStatusCode.NotFound);
            return query.Contains("alt=media") ? new(HttpStatusCode.OK) { Content = new ByteArrayContent(remote.Content) } : Json(remote.Json());
        }
        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }
}
