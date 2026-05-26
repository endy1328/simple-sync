namespace SimpleSync;

public sealed class FilterDialog : Form
{
    private readonly TextBox _extensionsInput = new();
    private readonly TextBox _filesInput = new();
    private readonly TextBox _includeInput = new();
    private readonly TextBox _excludeInput = new();
    private readonly CheckBox _includeSubdirectoriesCheck = new();
    private readonly ToolTip _toolTip = new();
    private readonly List<FilterPreset> _presets;
    private readonly List<FilterPreset> _selectedPresets = [];
    private readonly List<Button> _presetButtons = [];

    public FilterDialog(SyncPair pair)
        : this(pair, FilterPreset.CreateDefaults())
    {
    }

    public FilterDialog(SyncPair pair, IEnumerable<FilterPreset> presets)
    {
        Text = $"Filter for \"{(string.IsNullOrWhiteSpace(pair.Name) ? "Pair" : pair.Name.Trim())}\"";
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(980, 760);
        Size = new Size(1080, 820);
        ClientSize = new Size(1040, 780);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        Shown += (_, _) =>
        {
            if (ClientSize.Width < 1040 || ClientSize.Height < 780)
            {
                ClientSize = new Size(Math.Max(ClientSize.Width, 1040), Math.Max(ClientSize.Height, 780));
            }
        };

        IncludePatterns = [.. pair.IncludePatterns];
        ExcludePatterns = [.. pair.ExcludePatterns];
        Extensions = [.. pair.Extensions];
        Files = [.. pair.Files];
        IncludeSubdirectories = pair.IncludeSubdirectories;
        _presets = NormalizePresets(presets);

        BuildLayout();
        LoadValues();
    }

    public List<string> IncludePatterns { get; private set; } = [];
    public List<string> ExcludePatterns { get; private set; } = [];
    public List<string> Extensions { get; private set; } = [];
    public List<string> Files { get; private set; } = [];
    public bool IncludeSubdirectories { get; private set; } = true;

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var intro = new Label
        {
            Text = "Filter rules are applied per sync pair. Excluded files are not copied or deleted.\r\nPreset buttons stay active when clicked and append their configured values to the fields below.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(intro, 0, 0);

        var presetPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        presetPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        presetPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        presetPanel.Controls.Add(new Label
        {
            Text = "Quick presets: click one or more buttons to append suggested extensions or exclude patterns.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 6)
        }, 0, 0);

        var presets = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = Padding.Empty
        };
        foreach (var preset in _presets)
        {
            AddPresetButton(presets, preset);
        }
        presetPanel.Controls.Add(presets, 0, 1);
        root.Controls.Add(presetPanel, 0, 1);

        root.Controls.Add(CreateInputGroup("Extensions", "Separate values with comma, semicolon, or new line. Example: .md, .pdf", _extensionsInput), 0, 2);
        root.Controls.Add(CreateInputGroup("Specific files", "Relative file paths. Example: README.md, docs/setup.md", _filesInput), 0, 3);
        root.Controls.Add(CreateInputGroup("Include patterns", "Optional glob patterns. Empty means all files unless extensions or files are set.", _includeInput), 0, 4);
        root.Controls.Add(CreateInputGroup("Exclude patterns", "Exclude wins over every include rule.", _excludeInput), 0, 5);

        _includeSubdirectoriesCheck.Text = "Include subdirectories";
        _includeSubdirectoriesCheck.AutoSize = true;
        _includeSubdirectoriesCheck.Margin = new Padding(0, 10, 0, 10);
        root.Controls.Add(_includeSubdirectoriesCheck, 0, 6);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill
        };
        var applyButton = new Button { Text = "Apply", DialogResult = DialogResult.OK, AutoSize = true };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var resetButton = new Button { Text = "Reset", AutoSize = true };
        resetButton.Click += (_, _) => ResetValues();
        applyButton.Click += (_, _) => SaveValues();
        buttons.Controls.Add(applyButton);
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(resetButton);
        root.Controls.Add(buttons, 0, 7);

        AcceptButton = applyButton;
        CancelButton = cancelButton;
    }

    private static Control CreateInputGroup(string title, string hint, TextBox input)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 0, 0, 10),
            MinimumSize = new Size(0, 110)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold) }, 0, 0);
        panel.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = SystemColors.GrayText }, 0, 1);
        input.Dock = DockStyle.Fill;
        input.Multiline = true;
        input.MinimumSize = new Size(0, 72);
        input.ScrollBars = ScrollBars.Vertical;
        panel.Controls.Add(input, 0, 2);
        return panel;
    }

    private void AddPresetButton(FlowLayoutPanel parent, FilterPreset preset)
    {
        var button = new Button { Text = preset.Name, AutoSize = true, Margin = new Padding(0, 0, 6, 6), Padding = new Padding(8, 2, 8, 2) };
        _presetButtons.Add(button);
        _toolTip.SetToolTip(button, preset.Tooltip);
        button.Click += (_, _) =>
        {
            if (!_selectedPresets.Contains(preset))
            {
                _selectedPresets.Add(preset);
            }

            button.UseVisualStyleBackColor = false;
            button.BackColor = SystemColors.Highlight;
            button.ForeColor = SystemColors.HighlightText;
            ApplySelectedPresets();
        };
        parent.Controls.Add(button);
    }

    private void LoadValues()
    {
        _extensionsInput.Text = FormatValues(Extensions);
        _filesInput.Text = FormatValues(Files);
        _includeInput.Text = FormatValues(IncludePatterns);
        _excludeInput.Text = FormatValues(ExcludePatterns);
        _includeSubdirectoriesCheck.Checked = IncludeSubdirectories;
    }

    private void SaveValues()
    {
        Extensions = ParseLines(_extensionsInput.Text);
        Files = ParseLines(_filesInput.Text);
        IncludePatterns = ParseLines(_includeInput.Text);
        ExcludePatterns = ParseLines(_excludeInput.Text);
        IncludeSubdirectories = _includeSubdirectoriesCheck.Checked;
    }

    private void ResetValues()
    {
        _extensionsInput.Clear();
        _filesInput.Clear();
        _includeInput.Clear();
        _excludeInput.Clear();
        _includeSubdirectoriesCheck.Checked = true;
        _selectedPresets.Clear();
        foreach (var button in _presetButtons)
        {
            button.UseVisualStyleBackColor = true;
            button.ForeColor = SystemColors.ControlText;
        }
    }

    private static List<string> ParseLines(string text)
    {
        return FilterPreset.ParseValues(text);
    }

    private void ApplySelectedPresets()
    {
        _extensionsInput.Text = FormatValues(FilterPreset.MergeValues(_extensionsInput.Text, _selectedPresets, preset => preset.Extensions));
        _filesInput.Text = FormatValues(FilterPreset.MergeValues(_filesInput.Text, _selectedPresets, preset => preset.Files));
        _includeInput.Text = FormatValues(FilterPreset.MergeValues(_includeInput.Text, _selectedPresets, preset => preset.IncludePatterns));
        _excludeInput.Text = FormatValues(FilterPreset.MergeValues(_excludeInput.Text, _selectedPresets, preset => preset.ExcludePatterns));
    }

    private static string FormatValues(IEnumerable<string> values)
    {
        return string.Join(", ", values);
    }

    private static List<FilterPreset> NormalizePresets(IEnumerable<FilterPreset> presets)
    {
        var normalized = presets
            .Where(preset => !string.IsNullOrWhiteSpace(preset.Name))
            .ToList();

        return normalized.Count == 0 ? FilterPreset.CreateDefaults() : normalized;
    }
}
