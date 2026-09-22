namespace ValheimEditor;

public static class Appearance
{
    public static bool English { get; set; }
    public static bool Dark { get; set; } = true;
    public static string T(string ru, string en) => English ? en : ru;
    public static Color Background => Dark ? Color.FromArgb(13, 20, 26) : Color.FromArgb(236, 232, 223);
    public static Color Surface => Dark ? Color.FromArgb(22, 32, 40) : Color.FromArgb(248, 245, 238);
    public static Color Raised => Dark ? Color.FromArgb(31, 43, 52) : Color.White;
    public static Color Accent => Dark ? Color.FromArgb(231, 197, 131) : Color.FromArgb(138, 96, 36);
    public static Color TextColor => Dark ? Color.FromArgb(237, 241, 234) : Color.FromArgb(24, 33, 40);
    public static Color Muted => Dark ? Color.FromArgb(150, 175, 182) : Color.FromArgb(92, 106, 114);
    public static Color Border => Dark ? Color.FromArgb(56, 80, 88) : Color.FromArgb(196, 186, 168);
    public static Color Hover => Dark ? Color.FromArgb(42, 58, 68) : Color.FromArgb(232, 224, 208);
    public static Color Pressed => Dark ? Color.FromArgb(58, 72, 64) : Color.FromArgb(220, 210, 190);
    public static Font TitleFont { get; } = new("Segoe UI Semibold", 18f);
    public static Font HeadingFont { get; } = new("Segoe UI Semibold", 11f);
    public static Font BodyFont { get; } = new("Segoe UI", 10f);

    public static void Apply(Control control)
    {
        bool surface = control is Button or TextBoxBase or ListBox or NumericUpDown or ComboBox or ListView;
        control.BackColor = Equals(control.Tag, "accent-fill") ? Accent
            : Equals(control.Tag, "raised") ? Raised
            : surface || Equals(control.Tag, "surface") ? Surface
            : Background;
        control.ForeColor = Equals(control.Tag, "accent-fill") ? Color.FromArgb(17, 27, 33)
            : Equals(control.Tag, "accent") ? Accent
            : Equals(control.Tag, "muted") ? Muted
            : TextColor;
        if (control is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Equals(button.Tag, "accent-fill") ? Accent : Border;
            button.FlatAppearance.MouseOverBackColor = Equals(button.Tag, "accent-fill") ? Color.FromArgb(238, 214, 157) : Hover;
            button.FlatAppearance.MouseDownBackColor = Pressed;
            button.Cursor = Cursors.Hand;
        }
        foreach (Control child in control.Controls) Apply(child);
    }
}
