using SimpleSync;
using System.ComponentModel;
using System.Reflection;

public static class DriveDialogTests
{
    public static Task RunAsync()
    {
        Check(DrivePairDialog.ValidateSelection("local", "local", @"C:\source", @"D:\target", "", []) is null,
            "existing local pairs must not require an account");
        Check(DrivePairDialog.ValidateSelection("local", "google_drive", @"C:\source", "folder-id", "account", ["account"]) is null,
            "upload with connected account must be accepted");
        Check(DrivePairDialog.ValidateSelection("google_drive", "local", "folder-id", @"C:\target", "account", ["account"]) is null,
            "download with connected account must be accepted");
        Check(DrivePairDialog.ValidateSelection("google_drive", "google_drive", "one", "two", "account", ["account"]) is not null,
            "Drive to Drive is outside the supported scope");
        Check(DrivePairDialog.ValidateSelection("local", "google_drive", @"C:\source", "folder-id", "missing", ["account"]) is not null,
            "disconnected account must be rejected");
        Check(DrivePairDialog.ValidateSelection("local", "google_drive", @"C:\source", "", "account", ["account"]) is not null,
            "Drive folder selection is required");
        Check(DrivePairDialog.ValidateSelection("local", "local", "relative", @"C:\target", "", []) is not null,
            "relative PC paths must be rejected");
        Check(DrivePairDialog.ValidateSelection("invalid", "local", "folder", @"C:\target", "", []) is not null,
            "unknown endpoint types must be rejected");
        return CancelPendingFolderRequestBeforeClosing();
    }

    private static Task CancelPendingFolderRequestBeforeClosing()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: true);
                Application.EnableVisualStyles();
                using var host = new Form { StartPosition = FormStartPosition.Manual,
                    Location = new System.Drawing.Point(-12000, -12000), ShowInTaskbar = false };
                Exception? failure = null;
                host.Shown += (_, _) =>
                {
                    try
                    {
                        using var handler = new PendingRequestHandler();
                        using var client = new HttpClient(handler);
                        using var picker = new GoogleDriveFolderDialog(client, new LocalizationService())
                        {
                            StartPosition = FormStartPosition.Manual,
                            Location = new System.Drawing.Point(-12000, -12000),
                            ShowInTaskbar = false
                        };
                        picker.Show();
                        Application.DoEvents();
                        picker.Close();
                        var deadline = DateTime.UtcNow.AddSeconds(5);
                        while (!picker.IsDisposed && DateTime.UtcNow < deadline) Application.DoEvents();
                        Check(handler.Cancelled, "closing a folder picker must cancel its pending HTTP request");
                        Check(picker.IsDisposed, "folder picker must close after its pending request finishes cancellation");
                        BusyMainFormRejectsCustomPasteAndFilterEditing();
                        CreatesAndSelectsPickerFolder();
                        CancelsPendingFolderCreation();
                    }
                    catch (Exception ex) { failure = ex; }
                    finally { host.Close(); }
                };
                // Keep a real message loop alive across child-form disposal, just like the app.
                Application.Run(host);
                if (failure is not null) completion.SetException(failure);
                else completion.SetResult();
            }
            catch (Exception ex) { completion.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static void BusyMainFormRejectsCustomPasteAndFilterEditing()
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(MainForm);
        using var form = new MainForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-12000, -12000),
            ShowInTaskbar = false
        };
        // Keep all settings writes suppressed and never let this test start an automatic sync.
        type.GetField("_isLoadingConfig", privateInstance)!.SetValue(form, true);
        var autoTimer = (System.Windows.Forms.Timer)type.GetField("_timer", privateInstance)!.GetValue(form)!;
        autoTimer.Stop();
        var heading = (Label)type.GetField("_headerTitleLabel", privateInstance)!.GetValue(form)!;
        Check(heading.Text == "simple sync",
            "main heading must show only the app name; version belongs in the window title bar");
        Check(form.MainMenuStrip is not null, "main window must have a menu bar");
        var settings = form.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "설정");
        var accountsMenu = settings.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "구글 계정");
        Check(settings.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Text == "스킨") &&
            settings.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Text == "언어"), "application settings must be grouped under Settings");
        var pairs = (BindingList<SyncPair>)type.GetField("_pairs", privateInstance)!.GetValue(form)!;
        pairs.Clear();
        var pair = new SyncPair
        {
            Source = @"C:\unchanged-source", Target = @"D:\unchanged-target", Enabled = false,
            IncludePatterns = ["*.txt"]
        };
        pairs.Add(pair);
        var grid = (DataGridView)type.GetField("_grid", privateInstance)!.GetValue(form)!;
        // Bind the real grid without showing the main window: the handlers under test
        // require a selected bound cell, not a painted SplitContainer or an app loop.
        grid.BindingContext = new BindingContext();
        grid.DataSource = null;
        grid.DataSource = pairs;
        var sourceColumn = grid.Columns.Cast<DataGridViewColumn>()
            .Single(column => column.DataPropertyName == nameof(SyncPair.Source));
        grid.CurrentCell = grid.Rows[0].Cells[sourceColumn.Index];
        Check(ReferenceEquals(grid.CurrentRow?.DataBoundItem, pair), "busy editing regression must exercise a selected bound pair");
        type.GetMethod("SetBusy", privateInstance)!.Invoke(form, [true]);
        Check(grid.ReadOnly, "busy sync must make the grid read-only");
        Check(!accountsMenu.Enabled, "Google account settings must be disabled during synchronization");

        var gridPaste = new KeyEventArgs(Keys.Control | Keys.V);
        type.GetMethod("GridKeyDown", privateInstance)!.Invoke(form, [grid, gridPaste]);
        Check(!gridPaste.Handled && !gridPaste.SuppressKeyPress,
            "busy grid must return before intercepting Ctrl+V or reading clipboard content");

        using var editor = new TextBox { Text = "unchanged editor text" };
        editor.SelectAll();
        var editorPaste = new KeyEventArgs(Keys.Control | Keys.V);
        type.GetMethod("EditingTextBoxKeyDown", privateInstance)!.Invoke(form, [editor, editorPaste]);
        Check(!editorPaste.Handled && !editorPaste.SuppressKeyPress,
            "busy path editor must return before intercepting Ctrl+V or reading clipboard content");
        Check(editor.Text == "unchanged editor text" && pair.Source == @"C:\unchanged-source" && pair.Target == @"D:\unchanged-target",
            "busy custom paste handlers must preserve both the draft editor and bound endpoints");

        var modalOpened = false;
        using var modalWatchdog = new System.Windows.Forms.Timer { Interval = 25 };
        modalWatchdog.Tick += (_, _) =>
        {
            foreach (var modal in Application.OpenForms.Cast<Form>().OfType<FilterDialog>().ToList())
            {
                modalOpened = true;
                modal.Close();
            }
        };
        modalWatchdog.Start();
        type.GetMethod("EditSelectedFilter", privateInstance)!.Invoke(form, null);
        modalWatchdog.Stop();
        Check(!modalOpened, "busy filter command must return without opening a modal editor");
        Check(pair.IncludePatterns.SequenceEqual(["*.txt"]), "busy filter command must preserve the active sync filter");
        type.GetMethod("SetBusy", privateInstance)!.Invoke(form, [false]);
        Check(accountsMenu.Enabled, "account settings must be available after synchronization");
        Check(grid.GetClipboardContent()?.GetText(TextDataFormat.UnicodeText) == pair.Source,
            "copying the current source cell must not include other row values or column headers");
        pairs.Add(new SyncPair { Source = @"C:\other-row", Target = @"D:\other-target" });
        grid.Rows[1].Selected = true;
        var targetColumn = grid.Columns.Cast<DataGridViewColumn>()
            .Single(column => column.DataPropertyName == nameof(SyncPair.Target));
        grid.CurrentCell = grid.Rows[0].Cells[targetColumn.Index];
        Check(grid.GetClipboardContent()?.GetText(TextDataFormat.UnicodeText) == pair.Target,
            "multiple selected rows must still copy only the current target cell");
        grid.BeginEdit(true);
        Check(grid.EditingControl is TextBox, "copy regression must exercise an active text editor");
        ((TextBox)grid.EditingControl!).Text = @"D:\unsaved-target";
        Check(grid.GetClipboardContent()?.GetText(TextDataFormat.UnicodeText) == @"D:\unsaved-target",
            "copy in edit mode must use the current editor text rather than stale bound data");
        grid.CancelEdit();
        var localization = (LocalizationService)type.GetField("_localization", privateInstance)!.GetValue(form)!;
        localization.SetLanguage("en-US");
        type.GetMethod("ApplyLanguage", privateInstance)!.Invoke(form, null);
        Check(settings.Text == "Settings" && accountsMenu.Text == "Google accounts", "settings menu labels must update when language changes");
        var languageMenu = settings.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "Language");
        Check(languageMenu.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Checked).Tag as string == "en-US",
            "language submenu must check only the active language");
        var darkSkin = AppSkins.All.First(skin => skin.Key.Contains("dark"));
        type.GetMethod("ApplySkin", privateInstance)!.Invoke(form, [darkSkin]);
        var skinMenu = settings.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "Skin");
        Check(skinMenu.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Checked).Tag as string == darkSkin.Key &&
            form.MainMenuStrip.BackColor == darkSkin.Surface, "skin submenu and menu bar must reflect active skin");
        // Dispose directly rather than Close: MainForm.OnFormClosing intentionally saves settings.
    }

    private static void CreatesAndSelectsPickerFolder()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var server = new DriveEndpointTests.FakeDrive();
        server.Add("parent", "Parent", GoogleDriveEndpoint.FolderMimeType);
        server.Add("existing", "Existing", GoogleDriveEndpoint.FolderMimeType, parent: "parent");
        using var picker = new GoogleDriveFolderDialog(server.Client, new LocalizationService())
        { StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-12000, -12000), ShowInTaskbar = false };
        var type = typeof(GoogleDriveFolderDialog);
        var newButtonField = type.GetField("_newFolder", flags);
        Check(newButtonField is not null, "Drive folder picker must provide a New folder button");
        var button = (Button)newButtonField!.GetValue(picker)!;
        Check(!button.Enabled, "folder creation must remain disabled before the root is loaded");
        picker.Show();
        PumpUntil(() => button.Enabled, "Drive root must finish loading");
        var tree = (TreeView)type.GetField("_tree", flags)!.GetValue(picker)!;
        var parent = tree.Nodes[0].Nodes.Cast<TreeNode>().Single(node => node.Text == "Parent");
        tree.SelectedNode = parent;
        Check(button.Enabled, "folder creation must be enabled for a selected loaded Drive location");
        var task = (Task)type.GetMethod("CreateFolderAsync", flags)!.Invoke(picker, [parent, "Created from picker"])!;
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) Application.DoEvents();
        Check(task.IsCompleted, "folder creation must complete");
        task.GetAwaiter().GetResult();
        Check(tree.SelectedNode?.Text == "Created from picker" && tree.SelectedNode.Parent == parent,
            "new folder must appear under its parent and become selected: " +
            ((Label)type.GetField("_status", flags)!.GetValue(picker)!).Text);
        Check(parent.Nodes.Cast<TreeNode>().Any(node => node.Text == "Existing"), "creating under an unloaded parent must retain existing children");
        var id = tree.SelectedNode!.Tag as string;
        Check(id is not null && server.Files[id].Parent == "parent", "selected tree node must retain created remote folder ID");
        type.GetMethod("ChooseFolder", flags)!.Invoke(picker, null);
        Check(picker.DialogResult == DialogResult.OK && picker.SelectedFolderId == id && picker.SelectedFolderLabel.EndsWith("Parent/Created from picker"),
            "created folder must be selectable as the sync endpoint");
    }

    private static void CancelsPendingFolderCreation()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var server = new DriveEndpointTests.FakeDrive { BlockFolderCreation = true };
        using var picker = new GoogleDriveFolderDialog(server.Client, new LocalizationService())
        { StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-12000, -12000), ShowInTaskbar = false };
        picker.Show();
        var type = typeof(GoogleDriveFolderDialog);
        var tree = (TreeView)type.GetField("_tree", flags)!.GetValue(picker)!;
        var createButton = (Button)type.GetField("_newFolder", flags)!.GetValue(picker)!;
        PumpUntil(() => createButton.Enabled, "Drive root must finish loading before creation cancellation test");
        var task = (Task)type.GetMethod("CreateFolderAsync", flags)!.Invoke(picker, [tree.Nodes[0], "Cancelled folder"])!;
        PumpUntil(() => server.FolderCreationStarted, "folder creation request must reach the fake server");
        Check(!task.IsCompleted && !((Button)type.GetField("_newFolder", flags)!.GetValue(picker)!).Enabled,
            "creation must disable repeat submission while the API request is pending");
        picker.Close();
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while ((!task.IsCompleted || !picker.IsDisposed) && DateTime.UtcNow < deadline) Application.DoEvents();
        Check(task.IsCompleted && picker.IsDisposed && server.FolderCreationCancelled && server.Files.Count == 0,
            "closing during creation must cancel the pending request and defer disposal until completion");
        task.GetAwaiter().GetResult();
    }

    private static void PumpUntil(Func<bool> ready, string message)
    {
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (!ready() && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(1); }
        Check(ready(), message);
    }

    private sealed class PendingRequestHandler : HttpMessageHandler
    {
        public bool Cancelled { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
            throw new InvalidOperationException();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
