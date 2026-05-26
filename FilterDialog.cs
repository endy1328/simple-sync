namespace SimpleSync;

public sealed class FilterDialog : Form
{
    private readonly TextBox _extensionsInput = new();
    private readonly TextBox _filesInput = new();
    private readonly TextBox _includeInput = new();
    private readonly TextBox _excludeInput = new();
    private readonly CheckBox _includeSubdirectoriesCheck = new();

    public FilterDialog(SyncPair pair)
    {
        Text = $"Filter for \"{(string.IsNullOrWhiteSpace(pair.Name) ? "Pair" : pair.Name.Trim())}\"";
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(620, 560);
        Size = new Size(720, 640);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;

        IncludePatterns = [.. pair.IncludePatterns];
        ExcludePatterns = [.. pair.ExcludePatterns];
        Extensions = [.. pair.Extensions];
        Files = [.. pair.Files];
        IncludeSubdirectories = pair.IncludeSubdirectories;

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
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var intro = new Label
        {
            Text = "Filter rules are applied per sync pair. Excluded files are not copied or deleted.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(intro, 0, 0);

        var presets = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        AddPresetButton(presets, "Documents", [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".txt", ".md"], []);
        AddPresetButton(presets, "Images", [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg"], []);
        AddPresetButton(presets, "Code", [".cs", ".js", ".ts", ".py", ".java", ".xml", ".json", ".yml", ".md"], []);
        AddPresetButton(presets, "Archives", [".zip", ".7z", ".rar", ".tar", ".gz"], []);
        AddPresetButton(presets, "Exclude temp/build", [], ["*.tmp", "~*", ".git/**", "bin/**", "obj/**", "node_modules/**", ".vs/**"]);
        root.Controls.Add(presets, 0, 1);

        root.Controls.Add(CreateInputGroup("Extensions", "One extension per line, for example .md or pdf", _extensionsInput), 0, 2);
        root.Controls.Add(CreateInputGroup("Specific files", "One relative file path per line, for example README.md or docs/setup.md", _filesInput), 0, 3);
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
            Margin = new Padding(0, 0, 0, 8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold) }, 0, 0);
        panel.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = SystemColors.GrayText }, 0, 1);
        input.Dock = DockStyle.Fill;
        input.Multiline = true;
        input.ScrollBars = ScrollBars.Vertical;
        panel.Controls.Add(input, 0, 2);
        return panel;
    }

    private void AddPresetButton(FlowLayoutPanel parent, string text, string[] extensions, string[] excludes)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 0, 6, 6) };
        button.Click += (_, _) =>
        {
            if (extensions.Length > 0)
            {
                _extensionsInput.Text = MergeLines(_extensionsInput.Text, extensions);
            }

            if (excludes.Length > 0)
            {
                _excludeInput.Text = MergeLines(_excludeInput.Text, excludes);
            }
        };
        parent.Controls.Add(button);
    }

    private void LoadValues()
    {
        _extensionsInput.Text = string.Join(Environment.NewLine, Extensions);
        _filesInput.Text = string.Join(Environment.NewLine, Files);
        _includeInput.Text = string.Join(Environment.NewLine, IncludePatterns);
        _excludeInput.Text = string.Join(Environment.NewLine, ExcludePatterns);
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
    }

    private static List<string> ParseLines(string text)
    {
        return text
            .Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string MergeLines(string current, IEnumerable<string> additions)
    {
        var merged = ParseLines(current);
        foreach (var addition in additions)
        {
            if (!merged.Contains(addition, StringComparer.OrdinalIgnoreCase))
            {
                merged.Add(addition);
            }
        }

        return string.Join(Environment.NewLine, merged);
    }
}
