namespace SimpleSync;

internal sealed class NewDriveFolderDialog : Form
{
    private readonly TextBox _name = new() { Dock = DockStyle.Fill, MaxLength = 255 };
    public string FolderName { get; private set; } = "";

    public NewDriveFolderDialog(string parentPath, bool korean)
    {
        string T(string ko, string en) => korean ? ko : en;
        DriveDialogLayout.Initialize(this, T("새 폴더", "New folder"), new Size(500, 230));
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var parent = DriveDialogLayout.Label(T("생성 위치: ", "Create in: ") + parentPath);
        parent.AutoSize = false;
        parent.Height = 46;
        parent.AutoEllipsis = true;
        root.Controls.Add(parent, 0, 0);
        _name.PlaceholderText = T("폴더 이름", "Folder name");
        root.Controls.Add(_name, 0, 1);
        var error = DriveDialogLayout.Label("");
        error.ForeColor = Color.Firebrick;
        error.MaximumSize = new Size(450, 0);
        root.Controls.Add(error, 0, 2);
        var create = DriveDialogLayout.Button(T("만들기", "Create"));
        var cancel = DriveDialogLayout.Button(T("취소", "Cancel"));
        cancel.DialogResult = DialogResult.Cancel;
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.AddRange([create, cancel]);
        root.Controls.Add(footer, 0, 3);
        create.Click += (_, _) =>
        {
            try
            {
                FolderName = GoogleDriveEndpoint.NormalizeFolderName(_name.Text);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (IOException)
            {
                error.Text = T("올바른 폴더 이름을 입력하세요. 경로 구분자와 Windows 예약 이름은 사용할 수 없습니다.",
                    "Enter a valid folder name without path separators or Windows reserved names.");
                _name.Focus();
            }
        };
        Controls.Add(root);
        AcceptButton = create;
        CancelButton = cancel;
        Shown += (_, _) => _name.Focus();
    }
}
