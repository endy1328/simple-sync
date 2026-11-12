using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimpleSync;

public sealed partial class GoogleDriveEndpoint
{
    private sealed record InventorySnapshot(int Schema, string Scope, string RootId, string Cursor,
        DateTimeOffset FullScannedAt, Dictionary<string, List<RemoteFile>> Directories);
    private sealed record CacheEnvelope(string Payload, string Sha256);
    private sealed record ChangeSet(string Cursor, HashSet<string> Parents, bool Rebuild);
    public bool UsedIncrementalInventory { get; private set; }

    private async Task<IReadOnlyList<StorageEntry>> ListInventoryAsync(SyncFilter filter, bool forceFull, CancellationToken ct)
    {
        UsedIncrementalInventory = false;
        await ValidateRootAsync(ct);
        var scope = JsonSerializer.Serialize(new { accountId, rootFolderId, exportGoogleDocuments, Filter = filter.CacheKey });
        var path = Path.Combine(inventoryDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleSync", "DriveInventory"), Digest(scope) + ".json");
        var snapshot = forceFull ? null : ReadInventory(path, scope);
        if (snapshot is not null)
        {
            try
            {
                var changes = await GetChangesAsync(snapshot, ct);
                if (!changes.Rebuild)
                {
                    foreach (var parent in changes.Parents)
                        snapshot.Directories[parent] = (await GetChildrenAsync(parent, ct)).ToList();
                    var entries = await ScanAsync(filter, snapshot.Directories, true, ct, validateRoot: false);
                    ct.ThrowIfCancellationRequested();
                    SaveInventory(path, snapshot with { Cursor = changes.Cursor });
                    UsedIncrementalInventory = true;
                    return entries;
                }
            }
            catch (InvalidDataException) { /* An invalid/incomplete cursor cannot justify using old inventory. */ }
        }

        // Capture BEFORE scanning so mutations during the scan are replayed on the next run.
        var started = DateTimeOffset.UtcNow;
        var cursor = await GetStartCursorAsync(ct);
        var directories = new Dictionary<string, List<RemoteFile>>(StringComparer.Ordinal);
        var complete = await ScanAsync(filter, directories, false, ct, validateRoot: false);
        ct.ThrowIfCancellationRequested();
        SaveInventory(path, new(1, scope, _rootId!, cursor, started, directories));
        return complete;
    }

    private InventorySnapshot? ReadInventory(string path, string scope)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(File.ReadAllText(path));
            if (envelope is null || envelope.Payload is null || Digest(envelope.Payload) != envelope.Sha256) return null;
            var saved = JsonSerializer.Deserialize<InventorySnapshot>(envelope.Payload);
            if (saved is null || saved.Schema != 1 || saved.Scope != scope || saved.RootId != _rootId ||
                string.IsNullOrWhiteSpace(saved.Cursor) || saved.Directories is null || !saved.Directories.ContainsKey(_rootId!) ||
                saved.FullScannedAt > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow - saved.FullScannedAt > TimeSpan.FromDays(7)) return null;
            foreach (var directory in saved.Directories)
            {
                if (string.IsNullOrWhiteSpace(directory.Key) || directory.Value is null) return null;
                foreach (var file in directory.Value)
                    if (file is null || string.IsNullOrEmpty(file.Id) || string.IsNullOrEmpty(file.Name) ||
                        string.IsNullOrEmpty(file.MimeType) || file.Parent != directory.Key || file.Trashed || file.SharedDrive) return null;
            }
            return saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { return null; }
    }

    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static void SaveInventory(string path, InventorySnapshot snapshot)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var payload = JsonSerializer.Serialize(snapshot);
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(output, new CacheEnvelope(payload, Digest(payload)));
                output.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Cache storage is optional. */ }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private async Task<string> GetStartCursorAsync(CancellationToken ct)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get,
            "https://www.googleapis.com/drive/v3/changes/startPageToken?fields=startPageToken"), ct);
        await EnsureSuccessAsync(response, ct);
        using var json = await ReadJsonAsync(response, ct);
        var cursor = String(json.RootElement, "startPageToken");
        if (string.IsNullOrWhiteSpace(cursor)) throw new IOException("Drive returned no inventory start cursor.");
        return cursor;
    }

    private async Task<ChangeSet> GetChangesAsync(InventorySnapshot snapshot, CancellationToken ct)
    {
        var oldFiles = new Dictionary<string, RemoteFile>(StringComparer.Ordinal);
        foreach (var file in snapshot.Directories.Values.SelectMany(files => files))
            if (!oldFiles.TryAdd(file.Id, file)) throw new InvalidDataException("Repeated cached Drive file identity.");
        var parents = new HashSet<string>(StringComparer.Ordinal);
        var pages = new HashSet<string>(StringComparer.Ordinal);
        var cursor = snapshot.Cursor;
        var rebuild = false;
        while (pages.Add(cursor) && pages.Count <= 1000)
        {
            ct.ThrowIfCancellationRequested();
            var fields = $"nextPageToken,newStartPageToken,changes(fileId,removed,file({Fields}))";
            var url = "https://www.googleapis.com/drive/v3/changes?spaces=drive&includeRemoved=true&includeCorpusRemovals=true&pageSize=1000" +
                $"&pageToken={Escape(cursor)}&fields={Escape(fields)}";
            using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
            if (response.StatusCode is HttpStatusCode.Gone or HttpStatusCode.BadRequest)
                throw new InvalidDataException("Drive inventory cursor is no longer valid.");
            await EnsureSuccessAsync(response, ct);
            using var json = await ReadChangeJsonAsync(response, ct);
            var body = json.RootElement;
            if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Incomplete Drive change inventory.");
            foreach (var change in changes.EnumerateArray())
            {
                if (change.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Malformed Drive change entry.");
                var id = String(change, "fileId");
                if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Drive change has no identity.");
                oldFiles.TryGetValue(id, out var old);
                if (id == _rootId) { rebuild = true; continue; }
                if (old?.Parent is not null) parents.Add(old.Parent);
                if (old?.MimeType == FolderMimeType) rebuild = true;
                if (Bool(change, "removed")) continue;
                if (!change.TryGetProperty("file", out var item) || item.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("Drive change has no file metadata.");
                RemoteFile file;
                try { file = ParseFile(item); }
                catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
                { throw new InvalidDataException("Malformed Drive change metadata.", ex); }
                if (file.Id != id) throw new InvalidDataException("Drive change identity mismatch.");
                if (file.Parent is not null && snapshot.Directories.ContainsKey(file.Parent))
                {
                    parents.Add(file.Parent);
                    if (file.MimeType == FolderMimeType) rebuild = true;
                }
            }
            foreach (var key in new[] { "nextPageToken", "newStartPageToken" })
                if (body.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("Malformed Drive change cursor.");
            var next = String(body, "nextPageToken");
            var end = String(body, "newStartPageToken");
            if (!string.IsNullOrEmpty(next))
            {
                if (!string.IsNullOrEmpty(end)) throw new InvalidDataException("Ambiguous Drive change cursor.");
                cursor = next;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(end)) throw new InvalidDataException("Drive change feed has no completed cursor.");
                return new(end, parents, rebuild);
            }
        }
        throw new InvalidDataException("Drive change feed did not finish.");
    }

    private static async Task<JsonDocument> ReadChangeJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await ReadJsonAsync(response, ct); }
        catch (JsonException ex) { throw new InvalidDataException("Malformed Drive change feed.", ex); }
    }
}
