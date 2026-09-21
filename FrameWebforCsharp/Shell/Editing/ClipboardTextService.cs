namespace PDF_Manager.Shell.Editing;

public interface IClipboardTextService
{
    string GetText();

    void SetText(string text);
}

internal sealed class SystemClipboardTextService : IClipboardTextService
{
    public string GetText() => Clipboard.ContainsText(TextDataFormat.UnicodeText)
        ? Clipboard.GetText(TextDataFormat.UnicodeText)
        : string.Empty;

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Clipboard.SetText(text, TextDataFormat.UnicodeText);
    }
}
