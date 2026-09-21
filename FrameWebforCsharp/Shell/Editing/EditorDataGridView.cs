namespace PDF_Manager.Shell.Editing;

internal sealed class EditorDataGridView : DataGridView
{
    internal Func<Keys, bool>? CommandKeyHandler { get; set; }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Func<Keys, bool>? handler = CommandKeyHandler;
        return handler?.Invoke(keyData) == true || base.ProcessCmdKey(ref msg, keyData);
    }
}
