namespace PDF_Manager.Shell.ScreenComposition.Core;

internal static class ScreenCompositionStyle
{
    public static Color HeaderBackColor { get; } = Color.FromArgb(30, 37, 44);

    public static Color AccentColor { get; } = Color.FromArgb(80, 149, 252);

    public static Color NavigationBackColor { get; } = Color.FromArgb(248, 250, 252);

    public static Color SelectedBackColor { get; } = Color.FromArgb(220, 238, 250);

    public static Color BorderColor { get; } = Color.FromArgb(207, 216, 220);

    public static Color WorkspaceBackColor { get; } = Color.FromArgb(239, 244, 247);

    public static Button CreateFlatButton(string name, string text)
    {
        return new Button
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(45, 78, 108) },
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Name = name,
            TabStop = true,
            Text = text,
            UseVisualStyleBackColor = false,
        };
    }
}
