using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimpleSync;

/// <summary>Drive v3 transport for a single My Drive root. The caller owns the authenticated client.</summary>
public sealed partial class GoogleDriveEndpoint(HttpClient client, string rootFolderId, bool exportGoogleDocuments = true,
    string? accountId = null, string? inventoryDirectory = null) : IStorageEndpoint, IConcurrentTransferEndpoint, IConcurrentDirectoryEndpoint, IFreshInventoryEndpoint
{
    public GoogleDriveEndpoint(HttpClient client, string rootFolderId, bool exportGoogleDocuments)
        : this(client, rootFolderId, exportGoogleDocuments, null, null) { }

    public const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string Api = "https://www.googleapis.com/drive/v3/files";
    private const string UploadApi = "https://www.googleapis.com/upload/drive/v3/files";
    private const string Fields = "id,name,mimeType,parents,trashed,driveId,ownedByMe,size,md5Checksum,modifiedTime,version,capabilities(canEdit,canDelete,canListChildren)";
    private const int ChunkSize = 8 * 1024 * 1024;
    private readonly ConcurrentDictionary<string, RemoteFile> _known = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _folders = new(StringComparer.OrdinalIgnoreCase);
    private string? _rootId;
    public int MaxConcurrentTransfers => 4;
    public int MaxConcurrentDirectories => 4;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _folderGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _rootGate = new(1, 1), _idGate = new(1, 1);
    private readonly Queue<string> _reservedIds = new();

    private sealed record RemoteFile(string Id, string Name, string MimeType, string? Parent,
        bool Trashed, bool SharedDrive, bool OwnedByMe, long Size, string? Md5, string Version,
        bool CanEdit, bool CanDelete, bool CanListChildren);

    private static readonly IReadOnlyDictionary<string, (string Extension, string Mime)> Exports =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["application/vnd.google-apps.document"] = (".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            ["application/vnd.google-apps.spreadsheet"] = (".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            ["application/vnd.google-apps.presentation"] = (".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")
        };

    public Task<IReadOnlyList<StorageEntry>> ListAsync(SyncFilter filter, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(accountId) ? ScanAsync(filter, null, false, cancellationToken)
            : ListInventoryAsync(filter, forceFull: false, cancellationToken);

    public Task<IReadOnlyList<StorageEntry>> ListFreshAsync(SyncFilter filter, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(accountId) ? ScanAsync(filter, null, false, cancellationToken)
            : ListInventoryAsync(filter, forceFull: true, cancellationToken);

    private async Task<IReadOnlyList<StorageEntry>> ScanAsync(SyncFilter filter,
        Dictionary<string, List<RemoteFile>>? directories, bool reuse, CancellationToken cancellationToken, bool validateRoot = true)
    {
        if (validateRoot) await ValidateRootAsync(cancellationToken);
        _known.Clear();
        _reservedIds.Clear();
        _folders.Clear();
        _folderGates.Clear();
        _folders[""] = _rootId!;
        var result = new List<StorageEntry>();
        var pending = new Queue<(string Id, string Path)>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { _rootId! };
        pending.Enqueue((_rootId!, ""));
        while (pending.TryDequeue(out var directory))
        {
            IReadOnlyList<RemoteFile> children;
            if (reuse && directories!.TryGetValue(directory.Id, out var saved)) children = saved;
            else
            {
                children = await GetChildrenAsync(directory.Id, cancellationToken);
                if (directories is not null) directories[directory.Id] = children.ToList();
            }
            foreach (var file in children)
            {
                var rawPath = Join(directory.Path, file.Name);
                if (file.MimeType == FolderMimeType)
                {
                    if (!filter.ShouldTraverseDirectory(rawPath)) continue;
                    if (!visited.Add(file.Id)) throw new IOException("Drive folder traversal contains a repeated folder ID.");
                    ValidateSegment(file.Name);
                    if (_folders.ContainsKey(rawPath)) throw new IOException($"Duplicate Drive folder name: {rawPath}");
                    _folders[rawPath] = file.Id;
                    _known[file.Id] = file;
                    result.Add(ToEntry(file, rawPath));
                    if (!file.CanListChildren) throw new IOException($"Cannot list Drive folder: {rawPath}");
                    pending.Enqueue((file.Id, rawPath));
                }
                else
                {
                    var path = exportGoogleDocuments && Exports.TryGetValue(file.MimeType, out var export) ? rawPath + export.Extension : rawPath;
                    // Excluded unsupported formats must not stop otherwise valid filtered jobs.
                    if (!filter.ShouldCopyFile(path)) continue;
                    ValidateSegment(file.Name);
                    if (file.MimeType.StartsWith("application/vnd.google-apps.", StringComparison.Ordinal) && !Exports.ContainsKey(file.MimeType))
                        throw new IOException($"Unsupported Google document or shortcut: {rawPath}");
                    _known[file.Id] = file;
                    result.Add(ToEntry(file, path));
                }
            }
        }
        return result;
    }

    /// <summary>Lists folders using their original names for an interactive picker.</summary>
    public async Task<IReadOnlyList<StorageEntry>> ListChildrenAsync(string folderId, CancellationToken cancellationToken)
    {
        var folder = await GetFileAsync(folderId, cancellationToken);
        ValidateFolder(folder);
        if (!folder.CanListChildren) throw new IOException("Drive folder cannot be listed.");
        var children = await GetChildrenAsync(folder.Id, cancellationToken);
        return children.Where(file => file.MimeType == FolderMimeType && !file.SharedDrive)
            .Select(file => ToEntry(file, file.Name)).OrderBy(entry => entry.RelativePath, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<StorageEntry> CreateFolderAsync(string parentFolderId, string name, CancellationToken cancellationToken)
    {
        name = NormalizeFolderName(name);
        await ValidateRootAsync(cancellationToken);
        var parent = await GetFileAsync(parentFolderId, cancellationToken);
        ValidateFolder(parent);
        if (!parent.CanEdit || !parent.CanListChildren)
            throw new IOException("이 폴더 안에 새 폴더를 만들 권한이 없습니다. / Cannot create folders in this location.");
        var ancestor = parent;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (ancestor.Id != _rootId)
        {
            if (!visited.Add(ancestor.Id) || ancestor.Parent is null)
                throw new IOException("선택한 폴더가 내 드라이브 범위 밖에 있습니다. / Folder is outside the selected Drive root.");
            ancestor = await GetFileAsync(ancestor.Parent, cancellationToken);
            ValidateFolder(ancestor);
        }
        if ((await GetChildrenAsync(parent.Id, cancellationToken)).Any(file => string.Equals(file.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("같은 이름의 파일 또는 폴더가 있습니다. 다른 이름을 입력하세요. / A file or folder with this name already exists.");
        var folder = await CreateRemoteFolderAsync(parent.Id, name, cancellationToken);
        return ToEntry(folder, folder.Name);
    }

    public static string NormalizeFolderName(string name)
    {
        name = name.Trim();
        ValidateSegment(name);
        return StoragePath.Validate(name);
    }

    private async Task<RemoteFile> CreateRemoteFolderAsync(string parentId, string name, CancellationToken cancellationToken)
    {
        var id = await GenerateIdAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(new { id, name, mimeType = FolderMimeType, parents = new[] { parentId } });
        using var response = await SendAsync(() => JsonRequest(HttpMethod.Post, $"{Api}?fields={Escape(Fields)}", payload), cancellationToken);
        RemoteFile folder;
        if (response.StatusCode == HttpStatusCode.Conflict) folder = await GetFileAsync(id, cancellationToken);
        else
        {
            await EnsureSuccessAsync(response, cancellationToken);
            using var json = await ReadJsonAsync(response, cancellationToken);
            folder = ParseFile(json.RootElement);
        }
        ValidateFolder(folder);
        if (folder.Id != id || folder.Parent != parentId || folder.Name != name)
            throw new IOException("Created Drive folder identity did not match the requested parent and name.");
        return folder;
    }

    public async Task ReadAsync(StorageEntry entry, Stream destination, CancellationToken cancellationToken)
    {
        var file = await GetBoundFileAsync(entry, cancellationToken);
        if (file.MimeType == FolderMimeType) throw new IOException("A Drive folder cannot be downloaded as a file.");
        if (!string.IsNullOrEmpty(entry.Version) && file.Version != entry.Version)
            throw new IOException($"Drive source changed after listing: {entry.RelativePath}");
        var url = Exports.TryGetValue(file.MimeType, out var export)
            ? $"{Api}/{Escape(file.Id)}/export?mimeType={Escape(export.Mime)}"
            : $"{Api}/{Escape(file.Id)}?alt=media";
        if (file.MimeType.StartsWith("application/vnd.google-apps.", StringComparison.Ordinal) && !Exports.ContainsKey(file.MimeType))
            throw new IOException("Unsupported Google document format.");
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await response.Content.CopyToAsync(destination, cancellationToken);
        if (Exports.ContainsKey(file.MimeType) && (await GetFileAsync(file.Id, cancellationToken)).Version != file.Version)
            throw new IOException($"Google document changed during export: {entry.RelativePath}");
    }

    public async Task EnsureDirectoryAsync(string relativePath, CancellationToken cancellationToken)
    {
        var path = NormalizePath(relativePath, allowEmpty: true);
        if (_folders.ContainsKey(path)) return;
        await _rootGate.WaitAsync(cancellationToken);
        try
        {
            if (_rootId is null) await ValidateRootAsync(cancellationToken);
            _folders[""] = _rootId!;
        }
        finally { _rootGate.Release(); }
        var parent = _rootId!;
        var current = "";
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = Join(current, segment);
            if (_folders.TryGetValue(current, out var cached)) { parent = cached; continue; }
            // Only callers creating the same case-insensitive path wait for one another.
            // Release each parent lock before progressing to a child to avoid nested locks.
            var gate = _folderGates.GetOrAdd(current, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (_folders.TryGetValue(current, out cached)) { parent = cached; continue; }
                await ValidateParentAsync(parent, cancellationToken);
                var matches = (await GetChildrenAsync(parent, cancellationToken))
                    .Where(file => string.Equals(file.Name, segment, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count > 1 || matches.Any(file => file.MimeType != FolderMimeType))
                    throw new IOException($"Drive name conflicts with directory: {current}");
                var folder = matches.Count == 1 ? matches[0] : await CreateRemoteFolderAsync(parent, segment, cancellationToken);
                ValidateFolder(folder);
                _known[folder.Id] = folder;
                _folders[current] = folder.Id;
                parent = folder.Id;
            }
            finally { gate.Release(); }
        }
    }

    private async Task ValidateParentAsync(string id, CancellationToken cancellationToken)
    {
        await ValidateRootAsync(cancellationToken);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (id != _rootId)
        {
            if (!seen.Add(id)) throw new IOException("Drive folder ancestry contains a cycle.");
            var current = await GetFileAsync(id, cancellationToken);
            ValidateFolder(current);
            if (_known.TryGetValue(id, out var planned) && (current.Parent != planned.Parent || current.Name != planned.Name))
                throw new IOException("Drive ancestor moved or renamed during synchronization.");
            id = current.Parent ?? throw new IOException("Drive folder is outside the selected root.");
        }
    }

    public async Task WriteAsync(string relativePath, StorageEntry? existing, Stream source, CancellationToken cancellationToken)
    {
        if (!source.CanSeek) throw new IOException("Drive uploads require a seekable source stream.");
        var path = NormalizePath(relativePath, allowEmpty: false);
        var separator = path.LastIndexOf('/');
        var parentPath = separator < 0 ? "" : path[..separator];
        var name = separator < 0 ? path : path[(separator + 1)..];
        await EnsureDirectoryAsync(parentPath, cancellationToken);
        var parent = _folders[parentPath];
        await ValidateParentAsync(parent, cancellationToken);
        var children = await GetChildrenAsync(parent, cancellationToken);
        var matches = children.Where(file => string.Equals(file.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count > 1) throw new IOException($"Duplicate Drive filename: {path}");
        RemoteFile? target = null;
        if (existing is not null)
        {
            target = await GetBoundFileAsync(existing, cancellationToken);
            if (target.Parent != parent || !string.Equals(target.Name, name, StringComparison.OrdinalIgnoreCase) || matches.Count != 1 || matches[0].Id != target.Id)
                throw new IOException($"Drive target changed during synchronization: {path}");
        }
        else if (matches.Count > 0)
        {
            // Do not overwrite a file that appeared after planning.
            throw new IOException($"Drive target appeared during synchronization: {path}");
        }
        if (target is not null && (target.MimeType.StartsWith("application/vnd.google-apps.", StringComparison.Ordinal) || !target.CanEdit))
            throw new IOException($"Drive target cannot be replaced with an original-format file: {path}");

        var sourceStart = source.Position;
        var length = source.Length - sourceStart;
        var id = target?.Id ?? await GenerateIdAsync(cancellationToken);
        var metadata = target is null
            ? JsonSerializer.Serialize(new { id, name, mimeType = MimeForName(name), parents = new[] { parent } })
            : JsonSerializer.Serialize(new { name });
        var method = target is null ? HttpMethod.Post : HttpMethod.Patch;
        var uploadUrl = target is null ? UploadApi : $"{UploadApi}/{Escape(id)}";
        if (length <= 1024 * 1024)
        {
            var bytes = new byte[(int)length];
            source.Position = sourceStart;
            await source.ReadExactlyAsync(bytes, cancellationToken);
            using var response = await SendAsync(() =>
            {
                var parts = new MultipartContent("related");
                parts.Add(new StringContent(metadata, Encoding.UTF8, "application/json"));
                var media = new ByteArrayContent(bytes);
                media.Headers.ContentType = new MediaTypeHeaderValue(target?.MimeType ?? MimeForName(name));
                parts.Add(media);
                return new HttpRequestMessage(method, $"{uploadUrl}?uploadType=multipart&fields={Escape(Fields)}") { Content = parts };
            }, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict && target is null)
                await VerifyUploadedContentAsync(id, parent, name, source, sourceStart, cancellationToken);
            else
            {
                await EnsureSuccessAsync(response, cancellationToken);
                await VerifyUploadResponseAsync(response, id, parent, name, source, sourceStart, cancellationToken);
            }
            return;
        }
        using var initial = await SendAsync(() =>
        {
            var request = JsonRequest(method, $"{uploadUrl}?uploadType=resumable&fields={Escape(Fields)}", metadata);
            request.Headers.TryAddWithoutValidation("X-Upload-Content-Type", target?.MimeType ?? MimeForName(name));
            request.Headers.TryAddWithoutValidation("X-Upload-Content-Length", length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return request;
        }, cancellationToken);
        if (initial.StatusCode == HttpStatusCode.Conflict && target is null)
        {
            await VerifyUploadedContentAsync(id, parent, name, source, sourceStart, cancellationToken);
            return;
        }
        await EnsureSuccessAsync(initial, cancellationToken);
        var session = initial.Headers.Location;
        if (session is null || session.Scheme != Uri.UriSchemeHttps || session.Host != "www.googleapis.com")
            throw new IOException("Drive returned an invalid resumable upload endpoint.");

        var buffer = new byte[ChunkSize];
        long offset = 0;
        var recoveryAttempts = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            source.Position = sourceStart + offset;
            var count = (int)Math.Min(buffer.Length, length - offset);
            if (count > 0) await source.ReadExactlyAsync(buffer.AsMemory(0, count), cancellationToken);
            HttpResponseMessage? response = null;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, session);
                request.Content = new ByteArrayContent(buffer, 0, count);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(target?.MimeType ?? MimeForName(name));
                request.Content.Headers.TryAddWithoutValidation("Content-Range", count == 0 ? $"bytes */{length}" : $"bytes {offset}-{offset + count - 1}/{length}");
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    await VerifyUploadResponseAsync(response, id, parent, name, source, sourceStart, cancellationToken);
                    return;
                }
                if ((int)response.StatusCode == 308)
                {
                    var next = NextOffset(response, length);
                    if (next <= offset && ++recoveryAttempts > 3) throw new IOException("Drive upload made no progress.");
                    offset = next;
                    continue;
                }
                if (!await IsTransientAsync(response, cancellationToken)) await EnsureSuccessAsync(response, cancellationToken);
            }
            catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
            {
                // Query the session before retrying; a response can be lost after bytes were accepted.
            }
            finally { response?.Dispose(); }

            if (++recoveryAttempts > 3) throw new IOException("Drive upload failed after bounded recovery attempts.");
            await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << recoveryAttempts)), cancellationToken);
            using var status = await SendAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, session);
                request.Content = new ByteArrayContent([]);
                request.Content.Headers.TryAddWithoutValidation("Content-Range", $"bytes */{length}");
                return request;
            }, cancellationToken);
            if (status.IsSuccessStatusCode)
            {
                await VerifyUploadResponseAsync(status, id, parent, name, source, sourceStart, cancellationToken);
                return;
            }
            if ((int)status.StatusCode != 308) await EnsureSuccessAsync(status, cancellationToken);
            offset = NextOffset(status, length);
        }
    }

    public async Task DeleteAsync(StorageEntry entry, CancellationToken cancellationToken)
    {
        var file = await GetBoundFileAsync(entry, cancellationToken);
        if (!file.OwnedByMe || !file.CanDelete) throw new IOException($"Drive permanent deletion is not permitted: {entry.RelativePath}");
        if (file.MimeType == FolderMimeType && (await GetChildrenAsync(file.Id, cancellationToken)).Count > 0)
            return; // Empty-folder cleanup preserves excluded or newly created descendants.
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, $"{Api}/{Escape(file.Id)}"), cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound) await EnsureSuccessAsync(response, cancellationToken);
        _known.TryRemove(file.Id, out _);
        foreach (var key in _folders.Where(pair => pair.Value == file.Id).Select(pair => pair.Key).ToList()) _folders.TryRemove(key, out _);
    }

    private async Task ValidateRootAsync(CancellationToken cancellationToken)
    {
        var root = await GetFileAsync(_rootId ?? rootFolderId, cancellationToken);
        ValidateFolder(root);
        _rootId = root.Id;
    }

    private static void ValidateFolder(RemoteFile file)
    {
        if (file.Trashed || file.SharedDrive || file.MimeType != FolderMimeType)
            throw new IOException("The selected root must be an existing My Drive folder, outside the trash.");
    }

    private async Task<RemoteFile> GetBoundFileAsync(StorageEntry entry, CancellationToken cancellationToken)
    {
        await ValidateRootAsync(cancellationToken);
        if (!_known.TryGetValue(entry.Id, out var planned)) throw new IOException("Drive item was not part of the current endpoint snapshot.");
        var current = await GetFileAsync(entry.Id, cancellationToken);
        if (current.Trashed || current.SharedDrive || current.Parent != planned.Parent || current.Name != planned.Name || current.MimeType != planned.MimeType)
            throw new IOException($"Drive item changed location or type during synchronization: {entry.RelativePath}");
        var parent = current.Parent;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (parent != _rootId)
        {
            if (parent is null || !seen.Add(parent)) throw new IOException("Drive item is outside the selected root.");
            var ancestor = await GetFileAsync(parent, cancellationToken);
            ValidateFolder(ancestor);
            if (_known.TryGetValue(ancestor.Id, out var plannedAncestor) &&
                (ancestor.Parent != plannedAncestor.Parent || ancestor.Name != plannedAncestor.Name))
                throw new IOException("Drive ancestor moved or renamed during synchronization.");
            parent = ancestor.Parent;
        }
        return current;
    }

    private async Task<IReadOnlyList<RemoteFile>> GetChildrenAsync(string parent, CancellationToken cancellationToken)
    {
        var files = new List<RemoteFile>();
        string? pageToken = null;
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var query = $"'{parent.Replace("\\", "\\\\").Replace("'", "\\'")}' in parents and trashed = false";
            var url = $"{Api}?q={Escape(query)}&spaces=drive&pageSize=1000&fields={Escape($"nextPageToken,incompleteSearch,files({Fields})")}";
            if (pageToken is not null) url += "&pageToken=" + Escape(pageToken);
            using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            using var json = await ReadJsonAsync(response, cancellationToken);
            var body = json.RootElement;
            if (body.TryGetProperty("incompleteSearch", out var incomplete) && incomplete.GetBoolean()) throw new IOException("Drive returned an incomplete file listing.");
            if (!body.TryGetProperty("files", out var listed) || listed.ValueKind != JsonValueKind.Array) throw new IOException("Drive returned a malformed file listing.");
            foreach (var item in listed.EnumerateArray())
            {
                var file = ParseFile(item);
                if (file.Trashed || file.SharedDrive || file.Parent != parent) throw new IOException("Drive listing returned an unexpected parent or storage location.");
                files.Add(file);
            }
            pageToken = String(body, "nextPageToken");
            if (!string.IsNullOrEmpty(pageToken) && !tokens.Add(pageToken)) throw new IOException("Drive listing repeated a page token.");
        } while (!string.IsNullOrEmpty(pageToken));
        return files;
    }

    private async Task<RemoteFile> GetFileAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new IOException("Drive folder or file ID is missing.");
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"{Api}/{Escape(id)}?fields={Escape(Fields)}"), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var json = await ReadJsonAsync(response, cancellationToken);
        return ParseFile(json.RootElement);
    }

    private async Task<string> GenerateIdAsync(CancellationToken cancellationToken)
    {
        await _idGate.WaitAsync(cancellationToken);
        try
        {
            if (_reservedIds.Count == 0)
            {
                using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"{Api}/generateIds?count=100&space=drive&type=files"), cancellationToken);
                await EnsureSuccessAsync(response, cancellationToken);
                using var json = await ReadJsonAsync(response, cancellationToken);
                foreach (var value in json.RootElement.GetProperty("ids").EnumerateArray())
                    _reservedIds.Enqueue(value.GetString() is { Length: > 0 } id ? id : throw new IOException("Drive did not reserve a file ID."));
                if (_reservedIds.Count == 0) throw new IOException("Drive did not reserve a file ID.");
            }
            return _reservedIds.Dequeue();
        }
        finally { _idGate.Release(); }
    }

    private async Task VerifyUploadResponseAsync(HttpResponseMessage response, string id, string parent, string name, Stream source, long start, CancellationToken cancellationToken)
    {
        RemoteFile file;
        try
        {
            using var json = await ReadJsonAsync(response, cancellationToken);
            file = ParseFile(json.RootElement);
        }
        catch (Exception error) when (error is JsonException or IOException)
        {
            file = await GetFileAsync(id, cancellationToken);
        }
        await VerifyFileAsync(file, id, parent, name, source, start, cancellationToken);
    }

    private async Task VerifyUploadedContentAsync(string id, string parent, string name, Stream source, long start, CancellationToken cancellationToken)
    {
        var file = await GetFileAsync(id, cancellationToken);
        await VerifyFileAsync(file, id, parent, name, source, start, cancellationToken);
    }

    private async Task VerifyFileAsync(RemoteFile file, string id, string parent, string name, Stream source, long start, CancellationToken cancellationToken)
    {
        source.Position = start;
        var checksum = Convert.ToHexString(await MD5.HashDataAsync(source, cancellationToken));
        if (file.Id != id || file.Parent != parent || file.Name != name || file.Trashed || file.SharedDrive || file.Size != source.Length - start || !string.Equals(file.Md5, checksum, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Drive upload completed without a matching file identity and checksum.");
        _known[id] = file;
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = createRequest();
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < 3 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), cancellationToken);
                continue;
            }
            if (attempt >= 3 || !await IsTransientAsync(response, cancellationToken)) return response;
            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(250 * (1 << attempt));
            response.Dispose();
            await Task.Delay(delay > TimeSpan.FromSeconds(10) ? TimeSpan.FromSeconds(10) : delay < TimeSpan.Zero ? TimeSpan.Zero : delay, cancellationToken);
        }
    }

    private static async Task<bool> IsTransientAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500) return true;
        if (response.StatusCode != HttpStatusCode.Forbidden) return false;
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array && errors.EnumerateArray().Any(item => String(item, "reason") is "rateLimitExceeded" or "userRateLimitExceeded");
        }
        catch (JsonException) { return false; }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (response.IsSuccessStatusCode) return;
        var explanation = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Google account needs to sign in again.",
            HttpStatusCode.Forbidden => "Drive access denied, quota exceeded, or document export unavailable.",
            HttpStatusCode.NotFound => "Drive item or upload session no longer exists.",
            _ => "Drive request failed."
        };
        var reasons = new List<string>();
        string? project = null;
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                if (error.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                    foreach (var item in errors.EnumerateArray())
                        if (String(item, "reason") is { } reason) reasons.Add(reason);
                if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                    foreach (var item in details.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;
                        if (String(item, "reason") is { } reason) reasons.Add(reason);
                        if (item.TryGetProperty("metadata", out var metadata) && String(metadata, "consumer") is { } consumer &&
                            consumer.StartsWith("projects/", StringComparison.Ordinal) && consumer.Length is > 9 and < 40 &&
                            consumer[9..].All(char.IsAsciiDigit)) project = consumer[9..];
                    }
            }
        }
        catch (JsonException) { /* Non-JSON responses retain the HTTP-only diagnostic. */ }

        // Only known machine-readable codes and numeric project IDs are displayed.
        // Raw server messages, URLs, request headers and tokens never enter logs or UI.
        var guidance = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SERVICE_DISABLED"] = "OAuth 클라이언트와 같은 프로젝트에서 Google Drive API를 사용 설정하세요. / Enable Drive API in the OAuth client's project.",
            ["accessNotConfigured"] = "OAuth 클라이언트와 같은 프로젝트에서 Google Drive API를 사용 설정하세요. / Enable Drive API in the OAuth client's project.",
            ["ACCESS_TOKEN_SCOPE_INSUFFICIENT"] = "계정을 다시 연결하고 Drive 접근 권한을 승인하세요. / Reconnect and grant Drive access.",
            ["insufficientPermissions"] = "계정을 다시 연결하고 Drive 접근 권한을 승인하세요. / Reconnect and grant Drive access.",
            ["insufficientFilePermissions"] = "선택한 계정의 폴더 접근 권한을 확인하세요. / Check this account's folder permissions.",
            ["domainPolicy"] = "조직 정책으로 접근이 차단되었습니다. / Access blocked by organization policy.",
            ["rateLimitExceeded"] = "API 호출 제한입니다. 잠시 후 재시도하세요. / API rate limit; retry later.",
            ["userRateLimitExceeded"] = "계정별 API 호출 제한입니다. 잠시 후 재시도하세요. / Per-user rate limit; retry later.",
            ["dailyLimitExceeded"] = "프로젝트의 API 일일 할당량을 확인하세요. / Check project daily API quota.",
            ["quotaExceeded"] = "프로젝트의 API 할당량을 확인하세요. / Check project API quota.",
            ["storageQuotaExceeded"] = "Drive 저장 공간이 부족합니다. / Drive storage is full.",
            ["exportSizeLimitExceeded"] = "문서 내보내기 크기 제한을 초과했습니다. / Document export exceeds the size limit.",
            ["appNotAuthorizedToFile"] = "앱에 이 파일 접근 권한이 없습니다. / App is not authorized to access this file."
        };
        var known = reasons.Distinct(StringComparer.Ordinal).Where(guidance.ContainsKey).ToList();
        var detail = known.Count == 0 ? explanation : string.Join(Environment.NewLine, known.Select(reason => $"[{reason}] {guidance[reason]}"));
        if (project is not null) detail += $"\nGoogle Cloud 프로젝트 번호 / Project number: {project}";
        throw new IOException($"{detail}\nHTTP {(int)response.StatusCode}.");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static RemoteFile ParseFile(JsonElement json)
    {
        var id = String(json, "id") ?? throw new IOException("Drive file ID missing.");
        var name = String(json, "name") ?? throw new IOException("Drive filename missing.");
        var mime = String(json, "mimeType") ?? throw new IOException("Drive MIME type missing.");
        var parent = json.TryGetProperty("parents", out var parents) && parents.GetArrayLength() == 1 ? parents[0].GetString() : null;
        var capability = json.TryGetProperty("capabilities", out var capabilities) ? capabilities : default;
        return new(id, name, mime, parent, Bool(json, "trashed"), String(json, "driveId") is not null, Bool(json, "ownedByMe"),
            long.TryParse(String(json, "size"), out var size) ? size : 0, String(json, "md5Checksum"),
            (String(json, "version") ?? "") + ":" + (String(json, "modifiedTime") ?? ""),
            Bool(capability, "canEdit"), Bool(capability, "canDelete"), Bool(capability, "canListChildren"));
    }

    private static string? String(JsonElement json, string key) => json.ValueKind == JsonValueKind.Object && json.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool Bool(JsonElement json, string key) => json.ValueKind == JsonValueKind.Object && json.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.True;
    private static StorageEntry ToEntry(RemoteFile file, string path) => new(file.Id, path, file.MimeType == FolderMimeType, file.Size, file.Md5, file.Version, file.MimeType);
    private static string Escape(string value) => Uri.EscapeDataString(value);
    private static string Join(string directory, string name) => directory.Length == 0 ? name : directory + "/" + name;
    private static HttpRequestMessage JsonRequest(HttpMethod method, string url, string json) => new(method, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string NormalizePath(string path, bool allowEmpty)
    {
        if (allowEmpty && path.Length == 0) return "";
        var segments = path.Split('/');
        foreach (var segment in segments) ValidateSegment(segment);
        return string.Join('/', segments);
    }

    private static void ValidateSegment(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Contains('/') || name.Contains('\\') || name.Contains('\0'))
            throw new IOException("Drive filename cannot be represented as a safe relative path.");
    }

    private static long NextOffset(HttpResponseMessage response, long length)
    {
        if (!response.Headers.TryGetValues("Range", out var ranges)) return 0;
        var range = ranges.SingleOrDefault();
        var end = range?.LastIndexOf('-') ?? -1;
        if (range is null || !range.StartsWith("bytes=0-", StringComparison.Ordinal) || end < 0 || !long.TryParse(range[(end + 1)..], out var offset) || offset < 0 || offset >= length)
            throw new IOException("Drive returned an invalid resumable upload offset.");
        return offset + 1;
    }

    private static string MimeForName(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".pdf" => "application/pdf",
        ".txt" or ".md" or ".csv" => "text/plain",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };
}
