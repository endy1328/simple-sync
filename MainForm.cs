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
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(244, 247, 251);
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
            RowCount = 5,
            Padding = new Padding(16),
            BackColor = Color.FromArgb(244, 247, 251)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(CreateHeader(), 0, 0);

        var toolbar = new FlowLayoutPanel
        {
            BackColor = Color.White,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 10)
        };

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
        StyleButton(_nowButton, primary: true);
        _nowButton.Click += async (_, _) => await RunSyncAsync("수동 실행");

        _addButton.Text = "Add Pair";
        StyleButton(_addButton, primary: false);
        _addButton.Click += (_, _) =>
        {
            _pairs.Add(new SyncPair());
            SaveConfig();
        };

        _removeButton.Text = "Remove";
        StyleButton(_removeButton, primary: false);
        _removeButton.Click += (_, _) => RemoveSelectedRows();

        _browseSourceButton.Text = "Choose Source";
        StyleButton(_browseSourceButton, primary: false);
        _browseSourceButton.Click += (_, _) => BrowseSelectedPath(isSource: true);

        _browseTargetButton.Text = "Choose Target";
        StyleButton(_browseTargetButton, primary: false);
        _browseTargetButton.Click += (_, _) => BrowseSelectedPath(isSource: false);

        toolbar.Controls.Add(new Label
        {
            Text = "Every",
            AutoSize = true,
            ForeColor = Color.FromArgb(52, 64, 84),
            Padding = new Padding(0, 7, 2, 0)
        });
        toolbar.Controls.Add(_intervalInput);
        toolbar.Controls.Add(new Label
        {
            Text = "sec",
            AutoSize = true,
            ForeColor = Color.FromArgb(52, 64, 84),
            Padding = new Padding(0, 7, 12, 0)
        });
        toolbar.Controls.Add(_autoSyncCheck);
        toolbar.Controls.Add(_nowButton);
        toolbar.Controls.Add(_addButton);
        toolbar.Controls.Add(_removeButton);
        toolbar.Controls.Add(_browseSourceButton);
        toolbar.Controls.Add(_browseTargetButton);
        root.Controls.Add(toolbar, 0, 1);

        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.GridColor = Color.FromArgb(229, 234, 242);
        _grid.RowHeadersVisible = false;
        _grid.RowTemplate.Height = 34;
        _grid.ColumnHeadersHeight = 38;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 64, 84);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        _grid.DefaultCellStyle.BackColor = Color.White;
        _grid.DefaultCellStyle.ForeColor = Color.FromArgb(29, 41, 57);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(17, 24, 39);
        _grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 255);
        _grid.DataSource = _pairs;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(SyncPair.Enabled), HeaderText = "On", Width = 64 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SyncPair.Source), HeaderText = "Source", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Direction", HeaderText = "", ReadOnly = true, Width = 54, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SyncPair.Target), HeaderText = "Target", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
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
        root.Controls.Add(CreateSection("Sync pairs", _grid), 0, 2);

        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.BackColor = Color.White;
        _log.BorderStyle = BorderStyle.None;
        _log.ForeColor = Color.FromArgb(52, 64, 84);
        _log.Font = new Font("Consolas", 9F);
        _log.Margin = new Padding(0);
        root.Controls.Add(CreateSection("Activity", _log), 0, 3);

        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.FromArgb(71, 84, 103);
        _statusLabel.Padding = new Padding(2, 8, 0, 0);
        _statusLabel.Text = "Ready";
        root.Controls.Add(_statusLabel, 0, 4);
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

        titleBlock.Controls.Add(new Label
        {
            Text = "simple sync",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 24, 40),
            Margin = new Padding(0)
        }, 0, 0);
        titleBlock.Controls.Add(new Label
        {
            Text = "One-way file synchronization for local and network folders",
            AutoSize = true,
            ForeColor = Color.FromArgb(102, 112, 133),
            Margin = new Padding(1, 2, 0, 0)
        }, 0, 1);
        header.Controls.Add(titleBlock, 1, 0);

        return header;
    }

    private static Control CreateSection(string title, Control content)
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        section.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 24, 40),
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        content.Margin = new Padding(0);
        section.Controls.Add(content, 0, 1);
        return section;
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.AutoSize = true;
        button.Height = 32;
        button.Padding = new Padding(10, 4, 10, 4);
        button.Margin = new Padding(4, 0, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Color.FromArgb(21, 101, 216) : Color.FromArgb(208, 213, 221);
        button.BackColor = primary ? Color.FromArgb(33, 118, 255) : Color.White;
        button.ForeColor = primary ? Color.White : Color.FromArgb(52, 64, 84);
        button.UseVisualStyleBackColor = false;
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
            foreach (var pair in config.Pairs.Where(HasAnyPath))
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
            Pairs = _pairs.Where(HasAnyPath).ToList()
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
