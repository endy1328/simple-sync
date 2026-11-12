namespace SimpleSync;

/// <summary>Keeps row selection for pair management while copying only the current cell.</summary>
public sealed class CellTextDataGridView : DataGridView
{
    public override DataObject? GetClipboardContent()
    {
        if (CurrentCell is null) return null;
        var text = IsCurrentCellInEditMode && EditingControl is TextBox editor
            ? editor.Text
            : CurrentCell.FormattedValue?.ToString() ?? string.Empty;
        var content = new DataObject();
        content.SetData(DataFormats.UnicodeText, autoConvert: false, text);
        content.SetData(DataFormats.Text, autoConvert: false, text);
        return content;
    }
}
