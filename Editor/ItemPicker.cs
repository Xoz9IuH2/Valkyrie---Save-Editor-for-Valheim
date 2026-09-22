using static ValheimEditor.Appearance;

namespace ValheimEditor;

public sealed class RangeInput : UserControl
{
    private readonly TrackBar slider;
    private readonly NumericUpDown number;
    public decimal Value => number.Value;
    public event Action<decimal>? Changed;
    public RangeInput(int min, int max, decimal value, int decimals = 0)
    {
        Height = 44;
        number = new NumericUpDown { Dock = DockStyle.Right, Width = 100, Minimum = min, Maximum = max, DecimalPlaces = decimals, Value = Math.Clamp(value, min, max) };
        slider = new TrackBar { Dock = DockStyle.Fill, Minimum = min, Maximum = max, TickStyle = TickStyle.None, Value = (int)number.Value, SmallChange = 1, LargeChange = Math.Max(1, (max - min) / 10) };
        Controls.Add(slider); Controls.Add(number);
        slider.Scroll += (_, _) => number.Value = slider.Value;
        number.ValueChanged += (_, _) => { slider.Value = (int)number.Value; Changed?.Invoke(number.Value); };
    }
}

public sealed class ItemPicker : Form
{
    private readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Panel stats = new() { Dock = DockStyle.Left, Width = 280, Padding = new Padding(12), AutoScroll = true, Tag = "surface" };
    private readonly FlowLayoutPanel settings = new() { Dock = DockStyle.Right, Width = 310, Padding = new Padding(16), FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private RangeInput? amount, level;
    private readonly InventoryItem? existing;
    private readonly ComboBox category = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly CheckBox showInternal = new() { AutoSize = true };
    private string query = "";
    private static readonly string[] categories = ["All", "Weapons", "Armor", "Resources", "Food", "Other"];
    public ItemDefinition? Selection => list.SelectedItem as ItemDefinition;
    public int Quantity => (int)(amount?.Value ?? 1);
    public int Quality => (int)(level?.Value ?? 1);
    public bool Delete { get; private set; }

    public ItemPicker(InventoryItem? item)
    {
        existing = item;
        Text = item == null ? T("Добавить предмет", "Add item") : T("Изменить ячейку", "Edit slot");
        Width = 1180; Height = 720; MinimumSize = new Size(980, 580); StartPosition = FormStartPosition.CenterParent;
        Font = BodyFont; BackColor = Background; ForeColor = TextColor;
        var search = new TextBox { Dock = DockStyle.Top, Height = 32, PlaceholderText = T("Поиск по названию или ID: железо, SwordIron...", "Search by name or ID: iron, SwordIron...") };
        list.Tag = "surface"; stats.Tag = "raised"; settings.Tag = "raised";
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 64, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 10, 12, 10) };
        var apply = EditorForm.MakeButton(T("Применить", "Apply")); apply.Tag = "accent-fill";
        var cancel = EditorForm.MakeButton(T("Отмена", "Cancel")); cancel.DialogResult = DialogResult.Cancel;
        var remove = EditorForm.MakeButton(T("Очистить ячейку", "Clear slot")); remove.Enabled = item != null;
        actions.Controls.AddRange([apply, cancel, remove]);
        Controls.Add(list); Controls.Add(stats); Controls.Add(settings); Controls.Add(search); Controls.Add(actions);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
        category.Items.AddRange(English ? ["All", "Weapons", "Armor", "Resources", "Food", "Other"] : ["Все", "Оружие", "Броня", "Ресурсы", "Еда", "Прочее"]);
        category.SelectedIndex = 0;
        showInternal.Text = T("Показать служебные", "Show internal items");
        showInternal.Checked = item != null && Catalog.Find(item.Hash)?.Internal == true;
        filters.Controls.AddRange([category, showInternal]);
        Controls.Add(filters);
        category.SelectedIndexChanged += (_, _) => Filter(query);
        showInternal.CheckedChanged += (_, _) => Filter(query);
        list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = 56;
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            if ((e.State & DrawItemState.Selected) != 0)
            {
                using var fill = new SolidBrush(Raised);
                e.Graphics.FillRectangle(fill, e.Bounds);
            }
            var entry = (ItemDefinition)list.Items[e.Index];
            var icon = Catalog.ImageFor(entry);
            if (icon != null) e.Graphics.DrawImage(icon, new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 4, 48, 48));
            TextRenderer.DrawText(e.Graphics, entry.DisplayName, HeadingFont, new Rectangle(e.Bounds.X + 68, e.Bounds.Y + 6, e.Bounds.Width - 80, 28), TextColor, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, entry.Prefab, BodyFont, new Rectangle(e.Bounds.X + 68, e.Bounds.Y + 30, e.Bounds.Width - 80, 22), Muted, TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
        CancelButton = cancel;
        list.SelectedIndexChanged += (_, _) => ShowSettings();
        search.TextChanged += (_, _) => Filter(search.Text);
        Filter("");
        if (item != null)
        {
            list.SelectedItem = Catalog.Find(item.Hash);
            ShowSettings();
        }
        apply.Click += (_, _) =>
        {
            if (Selection == null) return;
            ValidateChildren();
            if (existing != null && existing.Hash != Selection.Hash && MessageBox.Show(T("Заменить предмет в этой ячейке? Его свойства будут сброшены.", "Replace this item? Its properties will be reset."), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            DialogResult = DialogResult.OK;
        };
        remove.Click += (_, _) =>
        {
            if (MessageBox.Show(T("Удалить предмет из ячейки?", "Delete the item from this slot?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            Delete = true; DialogResult = DialogResult.OK;
        };
        Appearance.Apply(this);
    }

    private void Filter(string query)
    {
        this.query = query;
        list.BeginUpdate();
        list.Items.Clear();
        list.Items.AddRange(Catalog.Filter(query, categories[category.SelectedIndex], showInternal.Checked).Cast<object>().ToArray());
        list.EndUpdate();
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        else ShowSettings();
    }

    private void ShowSettings()
    {
        settings.Controls.Clear(); stats.Controls.Clear(); amount = null; level = null;
        if (Selection is not { } definition) { Appearance.Apply(settings); return; }
        settings.Controls.Add(new Label { Text = definition.DisplayName, Width = 270, Height = 28, Font = HeadingFont, Tag = "accent" });
        settings.Controls.Add(new Label { Text = definition.Prefab, Width = 270, Height = 22, Tag = "muted" });
        bool same = definition.Hash == existing?.Hash;
        int quantity = same && int.TryParse(existing?.Quantity?.Value, out int n) ? n : 1;
        int quality = same && int.TryParse(existing?.Quality?.Value, out int q) ? q : 1;
        int maxQuality = QualityMode.MaxQuality(definition);
        if (quality > maxQuality) quality = maxQuality;
        settings.Controls.Add(new Label { Text = T("Количество · максимум ", "Quantity · maximum ") + definition.MaxStack, AutoSize = true });
        amount = new RangeInput(1, definition.MaxStack, quantity) { Width = 270 };
        settings.Controls.Add(amount);
        if (maxQuality > 1)
        {
            settings.Controls.Add(new Label { Text = T("Уровень · максимум ", "Level · maximum ") + maxQuality, AutoSize = true, Margin = new Padding(3, 24, 3, 3) });
            level = new RangeInput(1, maxQuality, Math.Min(quality, maxQuality)) { Width = 270 };
            settings.Controls.Add(level);
        }
        amount.Changed += _ => RenderStats();
        if (level != null) level.Changed += _ => RenderStats();
        string warning = definition.MaxQuality > 1 && QualityMode.Level != QualityLevel.Catalog
            ? T("Значения выше игрового максимума не проверены.", "Values above the in-game maximum are unverified.") + "\n\n"
            : "";
        settings.Controls.Add(new Label { Text = warning + T("Лимиты взяты из установленной игры.\n\nКаталог содержит также служебные варианты предметов. Проверяйте ID перед добавлением.", "Limits come from the installed game.\n\nThe catalog also includes internal item variants. Check the ID before adding."), Width = 270, Height = 140, Margin = new Padding(3, 24, 3, 3) });
        Appearance.Apply(settings);
        RenderStats();
    }

    private void RenderStats()
    {
        stats.Controls.Clear();
        if (Selection is not { } definition) return;
        string description = English ? definition.EnglishDescription : definition.Description;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 244 };
        panel.Controls.Add(new Label { Text = T("Характеристики", "Stats"), AutoSize = true, Tag = "accent", Font = HeadingFont });
        panel.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(description) ? definition.DisplayName : description, Width = 244, Height = 90 });
        foreach (var (label, value) in definition.Stats(Quality, Quantity))
            panel.Controls.Add(new Label { Text = label + ":  " + value, Width = 244, Height = 22 });
        stats.Controls.Add(panel);
        Appearance.Apply(stats);
    }
}
