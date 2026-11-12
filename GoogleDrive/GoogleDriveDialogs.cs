namespace SimpleSync;

internal static class DriveDialogLayout
{
    public static void Initialize(Form form, string title, Size size)
    {
        form.Text = title;
        form.Font = new Font("Segoe UI", 9F);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.ClientSize = size;
        form.MinimumSize = new Size(size.Width, size.Height);
        form.StartPosition = FormStartPosition.CenterParent;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
    }

    public static Button Button(string label) => new()
    {
        Text = label, AutoSize = true, MinimumSize = new Size(90, 32), Margin = new Padding(4)
    };

    public static Label Label(string text) => new()
    {
        Text = text, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(4, 8, 4, 6)
    };
}

public sealed class GoogleAccountsDialog : Form
{
    private readonly GoogleAccountService _service;
    private readonly bool _ko;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ListBox _accounts = new() { Dock = DockStyle.Fill, DisplayMember = nameof(GoogleAccount.Email) };
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Fill, MaximumSize = new Size(650, 0) };
    private readonly Button _import;
    private readonly Button _connect;
    private readonly Button _remove;
    private readonly Button _advanced;
    private bool _busy;
    private bool _closeRequested;

    public GoogleAccountsDialog(GoogleAccountService service, LocalizationService localization)
    {
        _service = service;
        _ko = localization.LanguageCode == "ko-KR";
        DriveDialogLayout.Initialize(this, T("Google 계정 관리", "Google accounts"), new Size(760, 560));
        _import = DriveDialogLayout.Button(T("OAuth 설정 가져오기", "Import OAuth settings"));
        _import.Visible = false;
        _import.Name = "ImportOAuthSettings";
        _connect = DriveDialogLayout.Button(T("Google 계정 연결", "Connect Google account"));
        _connect.Name = "ConnectGoogleAccount";
        _remove = DriveDialogLayout.Button(T("연결 해제", "Disconnect"));
        _advanced = DriveDialogLayout.Button(T("고급 설정", "Advanced settings"));
        _advanced.Name = "AdvancedGoogleSettings";

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var introduction = DriveDialogLayout.Label(T(
            "Google 계정 연결을 누르고 브라우저에서 개인 Google 계정을 선택한 뒤 Drive 접근을 허용하세요.\n연결된 계정으로 파일을 업로드하거나 다운로드할 수 있습니다. 여러 계정은 각각 연결하세요.",
            "Connect your Google account, choose a personal account in your browser and allow Drive access.\nUpload and download files with connected accounts. Connect each additional account separately."));
        introduction.MaximumSize = new Size(690, 0);
        root.Controls.Add(introduction, 0, 0);
        var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, WrapContents = true };
        actions.Controls.AddRange([_connect, _remove]);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(_accounts, 0, 2);
        root.Controls.Add(_status, 0, 3);
        var close = DriveDialogLayout.Button(T("닫기", "Close"));
        close.DialogResult = DialogResult.Cancel;
        var footer = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.Add(close);
        footer.Controls.Add(_advanced);
        footer.Controls.Add(_import);
        _advanced.Click += (_, _) => _import.Visible = !_import.Visible;
        root.Controls.Add(footer, 0, 4);
        Controls.Add(root);
        CancelButton = close;
        _import.Click += (_, _) => ImportSettings();
        _connect.Click += async (_, _) => await ConnectAsync();
        _remove.Click += (_, _) => Disconnect();
        _accounts.SelectedIndexChanged += (_, _) => UpdateButtons();
        FormClosing += (_, e) =>
        {
            _closeRequested = true;
            _lifetime.Cancel();
            if (_busy)
            {
                e.Cancel = true;
                DialogResult = DialogResult.None;
                _status.Text = T("계정 연결을 취소하는 중…", "Cancelling account connection…");
            }
        };
        RefreshAccounts();
    }

    private string T(string ko, string en) => _ko ? ko : en;

    private void RefreshAccounts(string? selectedId = null)
    {
        _accounts.Items.Clear();
        foreach (var account in _service.Accounts) _accounts.Items.Add(account);
        if (selectedId is not null)
        {
            for (var i = 0; i < _accounts.Items.Count; i++)
                if (((GoogleAccount)_accounts.Items[i]).Id == selectedId) _accounts.SelectedIndex = i;
        }
        if (_accounts.SelectedIndex < 0 && _accounts.Items.Count > 0) _accounts.SelectedIndex = 0;
        _status.Text = _service.HasClientConfiguration
            ? T("계정을 연결한 뒤 동기화 쌍의 연결 설정에서 선택하세요.",
                "Connect an account, then choose it in your sync pair's connection settings.")
            : T("이 빌드에는 Google 로그인 설정이 포함되지 않았습니다. 배포자에게 문의하거나 고급 설정을 사용하세요.",
                "Google sign-in is not configured in this build. Contact the distributor or use advanced settings.");
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _import.Enabled = !_busy;
        _advanced.Enabled = !_busy;
        _connect.Enabled = !_busy && _service.HasClientConfiguration;
        _remove.Enabled = !_busy && _accounts.SelectedItem is GoogleAccount;
        _accounts.Enabled = !_busy;
    }

    private void ImportSettings()
    {
        using var picker = new OpenFileDialog
        {
            Title = T("데스크톱 앱 OAuth JSON 선택", "Choose Desktop app OAuth JSON"),
            Filter = "JSON (*.json)|*.json", CheckFileExists = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _service.ImportClientConfiguration(picker.FileName);
            RefreshAccounts();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private async Task ConnectAsync()
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        _status.Text = T("브라우저에서 계정을 선택하고 접근을 허용하세요. 닫기를 누르면 대기를 취소합니다.",
            "Choose an account and grant access in your browser. Close this window to cancel waiting.");
        try
        {
            var account = await _service.ConnectAsync(_lifetime.Token);
            if (!IsDisposed && !_lifetime.IsCancellationRequested) RefreshAccounts(account.Id);
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !_lifetime.IsCancellationRequested) _status.Text = T("계정 연결을 취소했습니다.", "Account connection was cancelled.");
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !_lifetime.IsCancellationRequested) _status.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            if (!IsDisposed)
            {
                if (_closeRequested) Close();
                else UpdateButtons();
            }
        }
    }

    private void Disconnect()
    {
        if (_accounts.SelectedItem is not GoogleAccount account) return;
        try
        {
            _service.RemoveAccount(account.Id);
            RefreshAccounts();
            _status.Text = T("연결을 해제했습니다. 이 계정의 동기화 쌍은 다시 연결할 때까지 실행되지 않습니다.",
                "Disconnected. Sync pairs using this account need an account connection before they can run.");
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }
}

public sealed class DrivePairDialog : Form
{
    private readonly SyncPair _pair;
    private readonly GoogleAccountService _service;
    private readonly LocalizationService _localization;
    private readonly bool _ko;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ComboBox _direction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _account = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DisplayMember = nameof(GoogleAccount.Email) };
    private readonly TextBox _source = new() { Dock = DockStyle.Fill };
    private readonly TextBox _target = new() { Dock = DockStyle.Fill };
    private readonly Label _sourceLabel;
    private readonly Label _targetLabel;
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Fill, MaximumSize = new Size(720, 0) };
    private readonly Button _sourceBrowse;
    private readonly Button _targetBrowse;
    private readonly Button _manage;
    private readonly Button _save;
    private bool _loading;
    private bool _busy;
    private bool _closeRequested;
    private int _previousDirection;
    private string _localSource;
    private string _localTarget;
    private string _driveId;
    private string _driveLabel;
    private string _selectedAccountId;

    public DrivePairDialog(SyncPair pair, GoogleAccountService service, LocalizationService localization)
    {
        _pair = pair;
        _service = service;
        _localization = localization;
        _ko = localization.LanguageCode == "ko-KR";
        DriveDialogLayout.Initialize(this, T("동기화 연결 설정", "Sync connection"), new Size(860, 480));
        _localSource = pair.SourceType == StorageTypes.Local ? pair.Source : "";
        _localTarget = pair.TargetType == StorageTypes.Local ? pair.Target : "";
        _driveId = pair.SourceType == StorageTypes.GoogleDrive ? pair.Source : pair.TargetType == StorageTypes.GoogleDrive ? pair.Target : "";
        _driveLabel = pair.SourceType == StorageTypes.GoogleDrive ? pair.SourceLabel : pair.TargetLabel;
        _selectedAccountId = pair.DriveAccountId;
        _sourceLabel = DriveDialogLayout.Label("");
        _targetLabel = DriveDialogLayout.Label("");
        _sourceBrowse = DriveDialogLayout.Button(T("폴더 선택", "Choose folder"));
        _targetBrowse = DriveDialogLayout.Button(T("폴더 선택", "Choose folder"));
        _manage = DriveDialogLayout.Button(T("계정 관리", "Accounts"));
        _save = DriveDialogLayout.Button(T("적용", "Apply"));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3, RowCount = 7 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var i = 0; i < 4; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(DriveDialogLayout.Label(T("동기화 방향", "Direction")), 0, 0);
        root.Controls.Add(_direction, 1, 0);
        root.SetColumnSpan(_direction, 2);
        root.Controls.Add(DriveDialogLayout.Label(T("Google 계정", "Google account")), 0, 1);
        root.Controls.Add(_account, 1, 1);
        root.Controls.Add(_manage, 2, 1);
        root.Controls.Add(_sourceLabel, 0, 2);
        root.Controls.Add(_source, 1, 2);
        root.Controls.Add(_sourceBrowse, 2, 2);
        root.Controls.Add(_targetLabel, 0, 3);
        root.Controls.Add(_target, 1, 3);
        root.Controls.Add(_targetBrowse, 2, 3);
        var notice = DriveDialogLayout.Label(T(
            "소스 우선으로 반영합니다. 미러링은 타겟의 불필요한 파일을 영구 삭제합니다.\nDrive 문서는 Word·Excel·PowerPoint로 내려받고, 업로드는 원래 파일 형식을 유지합니다.",
            "The source takes priority. Mirror permanently deletes extra target files.\nGoogle documents download as Word, Excel or PowerPoint; uploads keep their original format."));
        notice.MaximumSize = new Size(780, 0);
        root.Controls.Add(notice, 0, 4);
        root.SetColumnSpan(notice, 3);
        root.Controls.Add(_status, 0, 5);
        root.SetColumnSpan(_status, 3);
        var cancel = DriveDialogLayout.Button(T("취소", "Cancel"));
        cancel.DialogResult = DialogResult.Cancel;
        var footer = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.AddRange([_save, cancel]);
        root.Controls.Add(footer, 0, 6);
        root.SetColumnSpan(footer, 3);
        Controls.Add(root);
        AcceptButton = _save;
        CancelButton = cancel;

        _loading = true;
        _direction.Items.AddRange([T("PC → PC", "PC → PC"), T("PC → Google Drive", "PC → Google Drive"), T("Google Drive → PC", "Google Drive → PC")]);
        _previousDirection = pair.SourceType == StorageTypes.GoogleDrive ? 2 : pair.TargetType == StorageTypes.GoogleDrive ? 1 : 0;
        _direction.SelectedIndex = _previousDirection;
        RefreshAccounts();
        _loading = false;
        RefreshDirection();
        _direction.SelectedIndexChanged += (_, _) => ChangeDirection();
        _account.SelectedIndexChanged += (_, _) => ChangeAccount();
        _sourceBrowse.Click += async (_, _) => await BrowseAsync(true);
        _targetBrowse.Click += async (_, _) => await BrowseAsync(false);
        _manage.Click += (_, _) => ManageAccounts();
        _save.Click += (_, _) => SaveSelection();
        FormClosing += (_, e) =>
        {
            _closeRequested = true;
            _lifetime.Cancel();
            if (_busy)
            {
                e.Cancel = true;
                DialogResult = DialogResult.None;
                _status.Text = T("연결을 취소하는 중…", "Cancelling connection…");
            }
        };
    }

    private string T(string ko, string en) => _ko ? ko : en;
    private bool SourceIsDrive => _direction.SelectedIndex == 2;
    private bool TargetIsDrive => _direction.SelectedIndex == 1;

    private void RefreshAccounts()
    {
        var previousLoading = _loading;
        _loading = true;
        _account.Items.Clear();
        foreach (var account in _service.Accounts)
        {
            _account.Items.Add(account);
            if (account.Id == _selectedAccountId) _account.SelectedItem = account;
        }
        if (_account.SelectedIndex < 0 && _account.Items.Count > 0 && string.IsNullOrEmpty(_selectedAccountId))
        {
            _account.SelectedIndex = 0;
            _selectedAccountId = ((GoogleAccount)_account.SelectedItem!).Id;
        }
        _loading = previousLoading;
    }

    private void CaptureLocalPaths()
    {
        if (_previousDirection != 2) _localSource = _source.Text;
        if (_previousDirection != 1) _localTarget = _target.Text;
    }

    private void ChangeDirection()
    {
        if (_loading) return;
        CaptureLocalPaths();
        _previousDirection = _direction.SelectedIndex;
        RefreshDirection();
    }

    private void ChangeAccount()
    {
        if (_loading) return;
        var id = (_account.SelectedItem as GoogleAccount)?.Id ?? "";
        if (id != _selectedAccountId)
        {
            _selectedAccountId = id;
            _driveId = "";
            _driveLabel = "";
        }
        CaptureLocalPaths();
        RefreshDirection();
    }

    private void RefreshDirection()
    {
        _sourceLabel.Text = SourceIsDrive ? T("소스 (Drive)", "Source (Drive)") : T("소스 (PC)", "Source (PC)");
        _targetLabel.Text = TargetIsDrive ? T("타겟 (Drive)", "Target (Drive)") : T("타겟 (PC)", "Target (PC)");
        _source.ReadOnly = SourceIsDrive;
        _target.ReadOnly = TargetIsDrive;
        var display = string.IsNullOrWhiteSpace(_driveId) ? "" : string.IsNullOrWhiteSpace(_driveLabel)
            ? T("선택된 Drive 폴더", "Selected Drive folder") : _driveLabel;
        _source.Text = SourceIsDrive ? display : _localSource;
        _target.Text = TargetIsDrive ? display : _localTarget;
        UpdateBusy();
    }

    private void UpdateBusy()
    {
        _direction.Enabled = !_busy;
        _account.Enabled = !_busy && (SourceIsDrive || TargetIsDrive);
        _manage.Enabled = !_busy;
        _source.Enabled = !_busy;
        _target.Enabled = !_busy;
        _sourceBrowse.Enabled = !_busy;
        _targetBrowse.Enabled = !_busy;
        _save.Enabled = !_busy;
    }

    private void ManageAccounts()
    {
        CaptureLocalPaths();
        using var dialog = new GoogleAccountsDialog(_service, _localization);
        dialog.ShowDialog(this);
        RefreshAccounts();
        RefreshDirection();
    }

    private async Task BrowseAsync(bool source)
    {
        if (_busy) return;
        var drive = source ? SourceIsDrive : TargetIsDrive;
        if (!drive)
        {
            using var picker = new FolderBrowserDialog
            {
                UseDescriptionForTitle = true,
                Description = source ? T("소스 PC 폴더 선택", "Choose source PC folder") : T("타겟 PC 폴더 선택", "Choose target PC folder"),
                SelectedPath = source ? _source.Text : _target.Text
            };
            if (picker.ShowDialog(this) == DialogResult.OK)
            {
                if (source) _source.Text = picker.SelectedPath;
                else _target.Text = picker.SelectedPath;
            }
            return;
        }
        if (_account.SelectedItem is not GoogleAccount account)
        {
            _status.Text = T("계정을 먼저 연결하고 선택하세요.", "Connect and select an account first.");
            return;
        }
        CaptureLocalPaths();
        _busy = true;
        UpdateBusy();
        _status.Text = T("Drive에 연결하는 중…", "Connecting to Drive…");
        try
        {
            using var client = await _service.CreateAuthenticatedClientAsync(account.Id, _lifetime.Token);
            if (IsDisposed || _lifetime.IsCancellationRequested) return;
            using var picker = new GoogleDriveFolderDialog(client, _localization);
            if (picker.ShowDialog(this) == DialogResult.OK)
            {
                _driveId = picker.SelectedFolderId;
                _driveLabel = picker.SelectedFolderLabel;
            }
            _status.Text = "";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!IsDisposed && !_lifetime.IsCancellationRequested) _status.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            if (!IsDisposed)
            {
                if (_closeRequested) Close();
                else RefreshDirection();
            }
        }
    }

    private void SaveSelection()
    {
        CaptureLocalPaths();
        var sourceType = SourceIsDrive ? StorageTypes.GoogleDrive : StorageTypes.Local;
        var targetType = TargetIsDrive ? StorageTypes.GoogleDrive : StorageTypes.Local;
        var source = SourceIsDrive ? _driveId : _localSource.Trim();
        var target = TargetIsDrive ? _driveId : _localTarget.Trim();
        var accountId = (_account.SelectedItem as GoogleAccount)?.Id ?? "";
        var error = ValidateSelection(sourceType, targetType, source, target, accountId, _service.Accounts.Select(a => a.Id));
        if (error is not null)
        {
            _status.Text = error switch
            {
                "account" => T("연결된 Google 계정을 선택하세요.", "Choose a connected Google account."),
                "path" => T("양쪽 폴더를 선택하고 PC 경로는 절대 경로로 입력하세요.", "Choose both folders and use absolute PC paths."),
                _ => T("지원하는 동기화 방향을 선택하세요.", "Choose a supported sync direction.")
            };
            return;
        }
        var connectionChanged = _pair.SourceType != sourceType || _pair.TargetType != targetType ||
            _pair.Source != source || _pair.Target != target ||
            ((SourceIsDrive || TargetIsDrive) && _pair.DriveAccountId != accountId);
        _pair.SourceType = sourceType;
        _pair.TargetType = targetType;
        _pair.Source = source;
        _pair.Target = target;
        _pair.SourceLabel = SourceIsDrive ? _driveLabel : "";
        _pair.TargetLabel = TargetIsDrive ? _driveLabel : "";
        _pair.DriveAccountId = SourceIsDrive || TargetIsDrive ? accountId : "";
        if (connectionChanged) _pair.Enabled = false;
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>Returns a stable validation code, or null for a supported complete selection.</summary>
    public static string? ValidateSelection(string sourceType, string targetType, string source, string target,
        string accountId, IEnumerable<string> connectedAccountIds)
    {
        if ((sourceType != StorageTypes.Local && sourceType != StorageTypes.GoogleDrive) ||
            (targetType != StorageTypes.Local && targetType != StorageTypes.GoogleDrive) ||
            (sourceType == StorageTypes.GoogleDrive && targetType == StorageTypes.GoogleDrive)) return "direction";
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) return "path";
        try
        {
            if ((sourceType == StorageTypes.Local && !Path.IsPathFullyQualified(source)) ||
                (targetType == StorageTypes.Local && !Path.IsPathFullyQualified(target))) return "path";
            if (sourceType == StorageTypes.Local) Path.GetFullPath(source);
            if (targetType == StorageTypes.Local) Path.GetFullPath(target);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return "path"; }
        if ((sourceType == StorageTypes.GoogleDrive || targetType == StorageTypes.GoogleDrive) &&
            (string.IsNullOrWhiteSpace(accountId) || !connectedAccountIds.Contains(accountId, StringComparer.Ordinal))) return "account";
        return null;
    }
}

public sealed class GoogleDriveFolderDialog : Form
{
    private readonly GoogleDriveEndpoint _endpoint;
    private readonly bool _ko;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, PathSeparator = "/" };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = true, MaximumSize = new Size(610, 0) };
    private readonly Button _choose;
    private readonly Button _newFolder;
    private bool _creating;
    private readonly HashSet<TreeNode> _loading = [];
    private bool _rootLoaded;
    private bool _closeRequested;
    private const string Placeholder = "loading";

    public string SelectedFolderId { get; private set; } = "";
    public string SelectedFolderLabel { get; private set; } = "";

    public GoogleDriveFolderDialog(HttpClient client, LocalizationService localization)
    {
        _ko = localization.LanguageCode == "ko-KR";
        _endpoint = new GoogleDriveEndpoint(client, "root");
        DriveDialogLayout.Initialize(this, T("내 드라이브 폴더 선택", "Choose a My Drive folder"), new Size(660, 520));
        _choose = DriveDialogLayout.Button(T("이 폴더 선택", "Choose this folder"));
        _choose.Enabled = false;
        _newFolder = DriveDialogLayout.Button(T("새 폴더", "New folder"));
        _newFolder.Enabled = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 3 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(_tree, 0, 0);
        root.Controls.Add(_status, 0, 1);
        var cancel = DriveDialogLayout.Button(T("취소", "Cancel"));
        cancel.DialogResult = DialogResult.Cancel;
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.AddRange([_choose, cancel, _newFolder]);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
        CancelButton = cancel;
        AcceptButton = _choose;
        var driveRoot = FolderNode(T("내 드라이브", "My Drive"), "root");
        _tree.Nodes.Add(driveRoot);
        _tree.SelectedNode = driveRoot;
        _tree.BeforeExpand += async (_, e) =>
        {
            if (e.Node is not null) await LoadNodeAsync(e.Node);
        };
        _tree.AfterSelect += (_, _) => UpdateChoose();
        _choose.Click += (_, _) => ChooseFolder();
        _newFolder.Click += async (_, _) =>
        {
            if (!_newFolder.Enabled || _tree.SelectedNode is not { Tag: string } parent) return;
            using var dialog = new NewDriveFolderDialog(parent.FullPath, _ko);
            if (dialog.ShowDialog(this) == DialogResult.OK)
                await CreateFolderAsync(parent, dialog.FolderName);
        };
        Shown += async (_, _) =>
        {
            await LoadNodeAsync(driveRoot);
            if (!IsDisposed && !_lifetime.IsCancellationRequested) driveRoot.Expand();
        };
        FormClosing += (_, e) =>
        {
            _closeRequested = true;
            _lifetime.Cancel();
            if (_loading.Count > 0 || _creating)
            {
                e.Cancel = true;
                DialogResult = DialogResult.None;
                _status.Text = T("폴더 작업을 취소하는 중…", "Cancelling folder operation…");
            }
        };
    }

    private string T(string ko, string en) => _ko ? ko : en;

    private static TreeNode FolderNode(string name, string id)
    {
        var node = new TreeNode(name) { Tag = id };
        node.Nodes.Add(new TreeNode("…") { Name = Placeholder });
        return node;
    }

    private void UpdateChoose()
    {
        _choose.Enabled = _newFolder.Enabled = _rootLoaded && !_creating && _loading.Count == 0 && _tree.SelectedNode?.Tag is string;
        _tree.Enabled = !_creating;
    }

    private async Task CreateFolderAsync(TreeNode parent, string name)
    {
        if (_creating || !_rootLoaded || _loading.Count > 0 || parent.Tag is not string parentId || _lifetime.IsCancellationRequested) return;
        _creating = true;
        UpdateChoose();
        try
        {
            await LoadNodeAsync(parent);
            if (IsDisposed || _lifetime.IsCancellationRequested) return;
            // A failed lazy load retains its placeholder; preserve its error and don't create blindly.
            if (parent.Nodes.Count == 1 && parent.Nodes[0].Name == Placeholder) return;
            _status.Text = T("폴더를 만드는 중…", "Creating folder…");
            var folder = await _endpoint.CreateFolderAsync(parentId, name, _lifetime.Token);
            if (IsDisposed || _lifetime.IsCancellationRequested) return;
            var node = FolderNode(folder.RelativePath, folder.Id);
            var index = parent.Nodes.Cast<TreeNode>().TakeWhile(child =>
                StringComparer.CurrentCultureIgnoreCase.Compare(child.Text, folder.RelativePath) <= 0).Count();
            parent.Nodes.Insert(index, node);
            parent.Expand();
            _tree.SelectedNode = node;
            node.EnsureVisible();
            _status.Text = T("폴더를 만들었습니다. ‘이 폴더 선택’을 눌러 적용하세요.", "Folder created. Click ‘Choose this folder’ to apply.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!IsDisposed && !_lifetime.IsCancellationRequested) _status.Text = ex.Message;
        }
        finally
        {
            _creating = false;
            if (!IsDisposed)
            {
                if (_closeRequested && _loading.Count == 0) Close();
                else if (!_closeRequested) UpdateChoose();
            }
        }
    }

    private async Task LoadNodeAsync(TreeNode node)
    {
        if (_loading.Contains(node) || node.Nodes.Count != 1 || node.Nodes[0].Name != Placeholder || node.Tag is not string id) return;
        _loading.Add(node);
        UpdateChoose();
        _status.Text = T("폴더를 불러오는 중…", "Loading folders…");
        try
        {
            var entries = await _endpoint.ListChildrenAsync(id, _lifetime.Token);
            if (IsDisposed || _lifetime.IsCancellationRequested) return;
            node.Nodes.Clear();
            foreach (var folder in entries.Where(e => e.IsDirectory).OrderBy(e => e.RelativePath, StringComparer.CurrentCultureIgnoreCase))
                node.Nodes.Add(FolderNode(folder.RelativePath, folder.Id));
            if (id == "root") _rootLoaded = true;
            _status.Text = T("동기화할 폴더를 선택하세요.", "Select the folder to sync.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!IsDisposed && !_lifetime.IsCancellationRequested) _status.Text = ex.Message;
        }
        finally
        {
            _loading.Remove(node);
            if (!IsDisposed)
            {
                if (_closeRequested && _loading.Count == 0) Close();
                else if (!_closeRequested) UpdateChoose();
            }
        }
    }

    private void ChooseFolder()
    {
        if (!_choose.Enabled || _tree.SelectedNode?.Tag is not string id) return;
        SelectedFolderId = id;
        SelectedFolderLabel = _tree.SelectedNode.FullPath;
        DialogResult = DialogResult.OK;
        Close();
    }
}
