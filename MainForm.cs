using System.ComponentModel;

namespace SimpleSync;

public sealed class MainForm : Form
{
    private sealed record ModeOption(string Value, string Label);

    private static readonly ModeOption[] ModeOptions =
    [
        new(SyncModes.Copy, "Copy changes"),
        new(SyncModes.Mirror, "Mirror source")
    ];

    private readonly BindingList<SyncPair> _pairs = [];
    private readonly ConfigService _configService;
    private readonly SyncService _syncService = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private CancellationTokenSource _syncCancellation = new();
    private readonly NumericUpDown _intervalInput = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _log = new();
    private readonly Button _nowButton = new();
    private readonly Button _addButton = new();
    private readonly Button _removeButton = new();
    private readonly Button _browseSourceButton = new();
    private readonly Button _browseTargetButton = new();
    private readonly CheckBox _autoSyncCheck = new();
    private readonly ComboBox _skinSelect = new();
    private readonly Label _statusLabel = new();
    private readonly List<Control> _surfaces = [];
    private readonly List<Label> _titleLabels = [];
    private readonly List<Label> _mutedLabels = [];
    private readonly List<Button> _primaryButtons = [];
    private readonly List<Button> _secondaryButtons = [];
    private AppSkin _currentSkin = AppSkins.Get(null);
    private bool _isLoadingConfig;

    public MainForm()
    {
        Text = "simple sync";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        Font = new Font("Segoe UI", 9F);
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
        _timer.Stop();
        _syncCancellation.Cancel();
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
            Padding = new Padding(16),
        };
        _surfaces.Add(root);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(CreateHeader(), 0, 0);

        var toolbar = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 10)
        };
        _surfaces.Add(toolbar);

        _intervalInput.Minimum = 1;
        _intervalInput.Maximum = 86_400;
        _intervalInput.Width = 90;
        _intervalInput.Height = 30;
        _intervalInput.Margin = new Padding(4, 2, 8, 0);
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
        _autoSyncCheck.Margin = new Padding(4, 6, 16, 0);
        _autoSyncCheck.CheckedChanged += (_, _) => ConfigureTimer();

        _nowButton.Text = "Sync Now";
        RegisterButton(_nowButton, primary: true);
        _nowButton.Click += async (_, _) => await RunSyncAsync("수동 실행");

        _addButton.Text = "Add Pair";
        RegisterButton(_addButton, primary: false);
        _addButton.Click += (_, _) =>
        {
            _pairs.Add(new SyncPair { Name = $"Pair {_pairs.Count + 1}" });
            SaveConfig();
        };

        _removeButton.Text = "Remove";
        RegisterButton(_removeButton, primary: false);
        _removeButton.Click += (_, _) => RemoveSelectedRows();

        _browseSourceButton.Text = "Choose Source";
        RegisterButton(_browseSourceButton, primary: false);
        _browseSourceButton.Click += (_, _) => BrowseSelectedPath(isSource: true);

        _browseTargetButton.Text = "Choose Target";
        RegisterButton(_browseTargetButton, primary: false);
        _browseTargetButton.Click += (_, _) => BrowseSelectedPath(isSource: false);

        toolbar.Controls.Add(CreateToolbarLabel("Every", new Padding(0, 7, 2, 0)));
        toolbar.Controls.Add(_intervalInput);
        toolbar.Controls.Add(CreateToolbarLabel("sec", new Padding(0, 7, 12, 0)));
        toolbar.Controls.Add(_autoSyncCheck);
        toolbar.Controls.Add(_nowButton);
        toolbar.Controls.Add(_addButton);
        toolbar.Controls.Add(_removeButton);
        toolbar.Controls.Add(_browseSourceButton);
        toolbar.Controls.Add(_browseTargetButton);

        _skinSelect.DropDownStyle = ComboBoxStyle.DropDownList;
        _skinSelect.Width = 150;
        _skinSelect.Margin = new Padding(16, 2, 0, 0);
        foreach (var skin in AppSkins.All)
        {
            _skinSelect.Items.Add(skin);
        }
        _skinSelect.SelectedIndexChanged += (_, _) =>
        {
            if (!_isLoadingConfig && _skinSelect.SelectedItem is AppSkin skin)
            {
                ApplySkin(skin);
                SaveConfig();
            }
        };
        toolbar.Controls.Add(CreateToolbarLabel("Skin", new Padding(16, 7, 2, 0)));
        toolbar.Controls.Add(_skinSelect);
        root.Controls.Add(toolbar, 0, 1);

        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeColumns = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.Dock = DockStyle.Fill;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.ScrollBars = ScrollBars.Both;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.RowHeadersVisible = false;
        _grid.RowTemplate.Height = 34;
        _grid.ColumnHeadersHeight = 38;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        _grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        _grid.DataSource = _pairs;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(SyncPair.Enabled), HeaderText = "On", Width = 64, MinimumWidth = 54 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SyncPair.Name), HeaderText = "Name", Width = 180, MinimumWidth = 120 });
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(SyncPair.Mode),
            HeaderText = "Mode",
            Width = 140,
            MinimumWidth = 120,
            DataSource = ModeOptions,
            ValueMember = nameof(ModeOption.Value),
            DisplayMember = nameof(ModeOption.Label),
            FlatStyle = FlatStyle.Flat
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SyncPair.Source), HeaderText = "Source", Width = 520, MinimumWidth = 220 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Direction", HeaderText = "", ReadOnly = true, Width = 44, MinimumWidth = 36, SortMode = DataGridViewColumnSortMode.NotSortable, Resizable = DataGridViewTriState.False });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SyncPair.Target), HeaderText = "Target", Width = 620, MinimumWidth = 220 });
        _grid.CellParsing += (_, e) =>
        {
            if (e.RowIndex >= 0 &&
                e.ColumnIndex >= 0 &&
                IsPathCell(_grid.Rows[e.RowIndex].Cells[e.ColumnIndex]) &&
                e.Value is null)
            {
                e.Value = string.Empty;
                e.ParsingApplied = true;
            }
        };
        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Columns[e.ColumnIndex].Name == "Direction")
            {
                e.Value = "->";
                e.FormattingApplied = true;
            }
        };
        _grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            e.Cancel = false;
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

        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.BorderStyle = BorderStyle.None;
        _log.Font = new Font("Consolas", 9F);
        _log.Margin = new Padding(0);

        var splitterInitialized = false;
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 7,
            Panel1MinSize = 140,
            Panel2MinSize = 120,
            Margin = new Padding(0, 0, 0, 10)
        };
        split.Panel1.Controls.Add(CreateSection("Sync pairs", _grid));
        split.Panel2.Controls.Add(CreateSection("Activity", _log));
        split.SizeChanged += (_, _) =>
        {
            if (splitterInitialized)
            {
                return;
            }

            var desired = Math.Max(split.Panel1MinSize, (int)(split.Height * 0.68));
            var max = split.Height - split.Panel2MinSize - split.SplitterWidth;
            if (max > split.Panel1MinSize)
            {
                split.SplitterDistance = Math.Min(desired, max);
                splitterInitialized = true;
            }
        };
        root.Controls.Add(split, 0, 2);

        _statusLabel.AutoSize = true;
        _mutedLabels.Add(_statusLabel);
        _statusLabel.Padding = new Padding(2, 8, 0, 0);
        _statusLabel.Text = "Ready";
        root.Controls.Add(_statusLabel, 0, 3);
    }

    private Label CreateToolbarLabel(string text, Padding padding)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Padding = padding
        };
        _mutedLabels.Add(label);
        return label;
    }

    private Control CreateHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var iconBox = new PictureBox
        {
            Image = Icon?.ToBitmap(),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Size = new Size(42, 42),
            Margin = new Padding(0, 2, 12, 0)
        };
        header.Controls.Add(iconBox, 0, 0);

        var titleBlock = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = new Padding(0)
        };
        titleBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = "simple sync",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            Margin = new Padding(0)
        };
        _titleLabels.Add(titleLabel);
        titleBlock.Controls.Add(titleLabel, 0, 0);

        var subtitleLabel = new Label
        {
            Text = "One-way file synchronization for local and network folders",
            AutoSize = true,
            Margin = new Padding(1, 2, 0, 0)
        };
        _mutedLabels.Add(subtitleLabel);
        titleBlock.Controls.Add(subtitleLabel, 0, 1);
        header.Controls.Add(titleBlock, 1, 0);

        return header;
    }

    private Control CreateSection(string title, Control content)
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };
        _surfaces.Add(section);
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };
        _titleLabels.Add(label);
        section.Controls.Add(label, 0, 0);

        content.Margin = new Padding(0);
        section.Controls.Add(content, 0, 1);
        return section;
    }

    private void RegisterButton(Button button, bool primary)
    {
        button.AutoSize = true;
        button.Height = 32;
        button.Padding = new Padding(10, 4, 10, 4);
        button.Margin = new Padding(4, 0, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.UseVisualStyleBackColor = false;
        if (primary)
        {
            _primaryButtons.Add(button);
        }
        else
        {
            _secondaryButtons.Add(button);
        }
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

    private static bool HasAnyPath(SyncPair pair)
    {
        return !string.IsNullOrWhiteSpace(pair.Source) || !string.IsNullOrWhiteSpace(pair.Target);
    }

    private static SyncPair NormalizePair(SyncPair pair, int index)
    {
        pair.Name = string.IsNullOrWhiteSpace(pair.Name) ? $"Pair {index + 1}" : pair.Name.Trim();
        pair.Mode = SyncModes.Normalize(pair.Mode);
        pair.Source ??= string.Empty;
        pair.Target ??= string.Empty;
        return pair;
    }

    private static string PairLogName(SyncPair pair)
    {
        return string.IsNullOrWhiteSpace(pair.Name) ? "Pair" : pair.Name.Trim();
    }

    private static string LogPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? "(empty)" : path.Trim();
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
            Size = new Size(
                Math.Max(MinimumSize.Width, config.WindowWidth),
                Math.Max(MinimumSize.Height, config.WindowHeight));
            ApplySkin(AppSkins.Get(config.Skin));

            _pairs.Clear();
            foreach (var pair in config.Pairs.Select(NormalizePair).Where(HasAnyPath))
            {
                _pairs.Add(pair);
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
            Skin = _currentSkin.Key,
            WindowWidth = WindowState == FormWindowState.Normal ? Width : RestoreBounds.Width,
            WindowHeight = WindowState == FormWindowState.Normal ? Height : RestoreBounds.Height,
            Pairs = _pairs.Select(NormalizePair).Where(HasAnyPath).ToList()
        });
    }

    private void ApplySkin(AppSkin skin)
    {
        _currentSkin = skin;
        BackColor = skin.AppBackground;

        foreach (var surface in _surfaces)
        {
            surface.BackColor = surface == _surfaces.FirstOrDefault() ? skin.AppBackground : skin.Surface;
        }

        foreach (var label in _titleLabels)
        {
            label.ForeColor = skin.Text;
        }

        foreach (var label in _mutedLabels)
        {
            label.ForeColor = skin.MutedText;
        }

        foreach (var button in _primaryButtons)
        {
            button.BackColor = skin.Accent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = skin.AccentDark;
        }

        foreach (var button in _secondaryButtons)
        {
            button.BackColor = skin.Surface;
            button.ForeColor = skin.Text;
            button.FlatAppearance.BorderColor = skin.Border;
        }

        _autoSyncCheck.ForeColor = skin.Text;
        _skinSelect.BackColor = skin.Surface;
        _skinSelect.ForeColor = skin.Text;
        _intervalInput.BackColor = skin.Surface;
        _intervalInput.ForeColor = skin.Text;

        _grid.BackgroundColor = skin.Surface;
        _grid.GridColor = skin.Border;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = skin.SurfaceAlt;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = skin.Text;
        _grid.DefaultCellStyle.BackColor = skin.Surface;
        _grid.DefaultCellStyle.ForeColor = skin.Text;
        _grid.DefaultCellStyle.SelectionBackColor = skin.AccentSoft;
        _grid.DefaultCellStyle.SelectionForeColor = skin.Text;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = skin.SurfaceAlt;

        _log.BackColor = skin.LogBackground;
        _log.ForeColor = skin.LogText;

        var selectedIndex = AppSkins.All.ToList().FindIndex(item => item.Key == skin.Key);
        if (selectedIndex >= 0 && _skinSelect.SelectedIndex != selectedIndex)
        {
            _skinSelect.SelectedIndex = selectedIndex;
        }
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
            _syncCancellation.Dispose();
            _syncCancellation = new CancellationTokenSource();
            AppendLog($"{reason} 시작");

            foreach (var pair in _pairs.Select(NormalizePair).Where(pair => pair.Enabled).ToList())
            {
                _syncCancellation.Token.ThrowIfCancellationRequested();

                var pairName = PairLogName(pair);
                AppendLog($"[{pairName}] 시작");
                AppendLog($"[{pairName}] 모드: {DescribeMode(pair.Mode)}");
                AppendLog($"[{pairName}] 진행경로: {LogPath(pair.Source)} -> {LogPath(pair.Target)}");
                var result = await _syncService.SyncAsync([pair], _syncCancellation.Token);
                AppendLog($"[{pairName}] 완료: 복사 {result.CopiedFiles}, 유지 {result.SkippedFiles}, 삭제 {result.DeletedFiles}, 실패 {result.FailedFiles}");

                foreach (var message in result.Messages)
                {
                    AppendLog($"[{pairName}] - {message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("동기화가 취소되었습니다.");
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

    private static string DescribeMode(string? mode)
    {
        return SyncModes.Normalize(mode) == SyncModes.Mirror
            ? "Mirror source"
            : "Copy changes";
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
