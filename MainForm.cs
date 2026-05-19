using System.ComponentModel;

namespace SimpleSync;

public sealed class MainForm : Form
{
    private readonly BindingList<SyncPair> _pairs = [];
    private readonly ConfigService _configService;
    private readonly SyncService _syncService = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly NumericUpDown _intervalInput = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _log = new();
    private readonly Button _nowButton = new();
    private readonly Button _addButton = new();
    private readonly Button _removeButton = new();
    private readonly Button _browseSourceButton = new();
    private readonly Button _browseTargetButton = new();
    private readonly CheckBox _autoSyncCheck = new();
    private readonly Label _statusLabel = new();
    private bool _isLoadingConfig;

    public MainForm()
    {
        Text = "simple sync";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        MinimumSize = new Size(860, 560);
        Size = new Size(1720, 1120);
        StartPosition = FormStartPosition.CenterScreen;

        _configService = new ConfigService(Path.Combine(AppContext.BaseDirectory, "config.toml"));

        BuildLayout();
        LoadConfig();
        ConfigureTimer();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveConfig(commitGridEdit: true);
        base.OnFormClosing(e);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var toolbar = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false
        };

        _intervalInput.Minimum = 1;
        _intervalInput.Maximum = 86_400;
        _intervalInput.Width = 90;
        _intervalInput.ValueChanged += (_, _) =>
        {
            if (_isLoadingConfig)
            {
                return;
            }

            ConfigureTimer();
            SaveConfig();
        };

        _autoSyncCheck.Text = "Auto";
        _autoSyncCheck.Checked = true;
        _autoSyncCheck.AutoSize = true;
        _autoSyncCheck.CheckedChanged += (_, _) => ConfigureTimer();

        _nowButton.Text = "Now";
        _nowButton.AutoSize = true;
        _nowButton.Click += async (_, _) => await RunSyncAsync("수동 실행");

        _addButton.Text = "Add";
        _addButton.AutoSize = true;
        _addButton.Click += (_, _) =>
        {
            _pairs.Add(new SyncPair());
            SaveConfig();
        };

        _removeButton.Text = "Remove";
        _removeButton.AutoSize = true;
        _removeButton.Click += (_, _) => RemoveSelectedRows();

        _browseSourceButton.Text = "Source...";
        _browseSourceButton.AutoSize = true;
        _browseSourceButton.Click += (_, _) => BrowseSelectedPath(isSource: true);

        _browseTargetButton.Text = "Target...";
        _browseTargetButton.AutoSize = true;
        _browseTargetButton.Click += (_, _) => BrowseSelectedPath(isSource: false);

        toolbar.Controls.Add(new Label { Text = "Interval (sec)", AutoSize = true, Padding = new Padding(0, 7, 4, 0) });
        toolbar.Controls.Add(_intervalInput);
        toolbar.Controls.Add(_autoSyncCheck);
        toolbar.Controls.Add(_nowButton);
        toolbar.Controls.Add(_addButton);
        toolbar.Controls.Add(_removeButton);
        toolbar.Controls.Add(_browseSourceButton);
        toolbar.Controls.Add(_browseTargetButton);
        root.Controls.Add(toolbar, 0, 0);

        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.Dock = DockStyle.Fill;
        _grid.DataSource = _pairs;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(SyncPair.Enabled), HeaderText = "Enabled", Width = 72 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SyncPair.Source), HeaderText = "Source", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SyncPair.Target), HeaderText = "Target", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            AppendLog($"입력 오류: {e.Exception?.Message ?? "알 수 없는 오류"}");
        };
        _grid.EditingControlShowing += (_, e) =>
        {
            if (e.Control is TextBox textBox)
            {
                textBox.KeyDown -= EditingTextBoxKeyDown;
                textBox.KeyDown += EditingTextBoxKeyDown;
            }
        };
        _grid.KeyDown += GridKeyDown;
        _grid.CellValueChanged += (_, _) =>
        {
            if (!_isLoadingConfig)
            {
                SaveConfig();
            }
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _grid.UserDeletedRow += (_, _) => SaveConfig();
        root.Controls.Add(_grid, 0, 1);

        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        root.Controls.Add(_log, 0, 2);

        _statusLabel.AutoSize = true;
        _statusLabel.Text = "Ready";
        root.Controls.Add(_statusLabel, 0, 3);
    }

    private void GridKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Control || e.KeyCode != Keys.V || !IsPathCell(_grid.CurrentCell))
        {
            return;
        }

        e.SuppressKeyPress = true;
        e.Handled = true;

        var text = GetClipboardPathText();
        if (string.IsNullOrWhiteSpace(text))
        {
            AppendLog("클립보드에 붙여넣을 텍스트 경로가 없습니다.");
            return;
        }

        if (_grid.CurrentRow?.DataBoundItem is not SyncPair pair)
        {
            return;
        }

        var propertyName = _grid.CurrentCell?.OwningColumn?.DataPropertyName;
        if (propertyName == nameof(SyncPair.Source))
        {
            pair.Source = text;
        }
        else
        {
            pair.Target = text;
        }

        _grid.Refresh();
        SaveConfig();
    }

    private void EditingTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Control || e.KeyCode != Keys.V || sender is not TextBox textBox || !IsPathCell(_grid.CurrentCell))
        {
            return;
        }

        e.SuppressKeyPress = true;
        e.Handled = true;

        var text = GetClipboardPathText();
        if (string.IsNullOrWhiteSpace(text))
        {
            AppendLog("클립보드에 붙여넣을 텍스트 경로가 없습니다.");
            return;
        }

        textBox.SelectedText = text;
    }

    private static bool IsPathCell(DataGridViewCell? cell)
    {
        var propertyName = cell?.OwningColumn?.DataPropertyName;
        return propertyName is nameof(SyncPair.Source) or nameof(SyncPair.Target);
    }

    private static string? GetClipboardPathText()
    {
        try
        {
            string? text = null;

            if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
            {
                text = Clipboard.GetText(TextDataFormat.UnicodeText);
            }
            else if (Clipboard.ContainsText())
            {
                text = Clipboard.GetText();
            }
            else if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                text = files.Count > 0 ? files[0] : null;
            }

            return NormalizeClipboardPath(text);
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeClipboardPath(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var firstLine = text
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return firstLine?.Trim().Trim('"');
    }

    private void LoadConfig()
    {
        _isLoadingConfig = true;
        try
        {
            var config = _configService.Load();
            _intervalInput.Value = Math.Clamp(config.IntervalSeconds, 1, 86_400);

            _pairs.Clear();
            foreach (var pair in config.Pairs)
            {
                _pairs.Add(pair);
            }

            if (_pairs.Count == 0)
            {
                _pairs.Add(new SyncPair());
            }
        }
        finally
        {
            _isLoadingConfig = false;
        }
    }

    private void SaveConfig()
    {
        SaveConfig(commitGridEdit: false);
    }

    private void SaveConfig(bool commitGridEdit)
    {
        if (_isLoadingConfig)
        {
            return;
        }

        if (commitGridEdit)
        {
            _grid.EndEdit();
        }

        _configService.Save(new AppConfig
        {
            IntervalSeconds = (int)_intervalInput.Value,
            Pairs = _pairs.ToList()
        });
    }

    private void ConfigureTimer()
    {
        _timer.Stop();
        _timer.Interval = (int)_intervalInput.Value * 1000;
        _timer.Tick -= TimerTick;
        _timer.Tick += TimerTick;

        if (_autoSyncCheck.Checked)
        {
            _timer.Start();
            _statusLabel.Text = $"Auto sync every {_intervalInput.Value} sec";
        }
        else
        {
            _statusLabel.Text = "Auto sync paused";
        }
    }

    private async void TimerTick(object? sender, EventArgs e)
    {
        await RunSyncAsync("자동 실행");
    }

    private async Task RunSyncAsync(string reason)
    {
        SaveConfig(commitGridEdit: true);

        if (!await _syncLock.WaitAsync(0))
        {
            AppendLog("이미 동기화가 실행 중입니다.");
            return;
        }

        SetBusy(true);
        try
        {
            AppendLog($"{reason} 시작");
            var result = await _syncService.SyncAsync(_pairs.ToList(), CancellationToken.None);
            AppendLog($"완료: 복사 {result.CopiedFiles}, 유지 {result.SkippedFiles}, 실패 {result.FailedFiles}");

            foreach (var message in result.Messages)
            {
                AppendLog($"- {message}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"실패: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            _syncLock.Release();
        }
    }

    private void RemoveSelectedRows()
    {
        var rows = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem)
            .OfType<SyncPair>()
            .ToList();

        foreach (var pair in rows)
        {
            _pairs.Remove(pair);
        }

        SaveConfig();
    }

    private void BrowseSelectedPath(bool isSource)
    {
        if (_grid.CurrentRow?.DataBoundItem is not SyncPair pair)
        {
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            UseDescriptionForTitle = true,
            Description = isSource ? "소스 폴더 선택" : "타겟 폴더 선택"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (isSource)
        {
            pair.Source = dialog.SelectedPath;
        }
        else
        {
            pair.Target = dialog.SelectedPath;
        }

        _grid.Refresh();
        SaveConfig();
    }

    private void SetBusy(bool busy)
    {
        _nowButton.Enabled = !busy;
        _statusLabel.Text = busy ? "Syncing..." : (_autoSyncCheck.Checked ? $"Auto sync every {_intervalInput.Value} sec" : "Auto sync paused");
    }

    private void AppendLog(string message)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
