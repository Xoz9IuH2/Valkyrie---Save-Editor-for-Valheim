namespace ValheimEditor;

public static class Appearance
{
    public static bool English { get; set; }
    public static bool Dark { get; set; } = true;
    public static string T(string ru, string en) => English ? en : ru;
    public static void Apply(Control control)
    {
        bool surface = control is Button or TextBoxBase or ListBox or NumericUpDown or ComboBox;
        control.BackColor = Dark ? (surface ? Color.FromArgb(47, 58, 61) : Color.FromArgb(24, 29, 32))
            : (surface ? Color.White : Color.FromArgb(240, 243, 246));
        control.ForeColor = Dark ? Color.FromArgb(232, 224, 203) : Color.FromArgb(28, 39, 49);
        foreach (Control child in control.Controls) Apply(child);
    }
}
