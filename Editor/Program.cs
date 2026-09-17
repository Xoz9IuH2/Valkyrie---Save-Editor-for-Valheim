using System.Diagnostics;
using static ValheimEditor.Appearance;

namespace ValheimEditor;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--test") return Tests.Run(args[1]);
        ApplicationConfiguration.Initialize();
        Application.Run(new EditorForm());
        return 0;
    }
}

public sealed class EditorForm : Form
{
    private readonly Label status = new() { AutoSize = true, Text = "Откройте персонажа. Исходный файл останется нетронутым.", Padding = new Padding(8) };
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill, Padding = new Point(24, 12) };
    private readonly FlowLayoutPanel skills = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
    private readonly TableLayoutPanel inventory = new() { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 4, Padding = new Padding(16) };
    private readonly TextBox characterName = new() { Width = 240 };
    private SaveFile? save;
    private string source = "";
    private bool dirty;
    private bool refreshing;
    private readonly Stack<Action> undo = new();
    private readonly Button undoButton = MakeButton("");
    private readonly Button restoreButton = MakeButton("");
    private readonly Button clearFlagsButton = MakeButton("");
    private readonly Button open = MakeButton("");
    private readonly Button export = MakeButton("");
    private readonly Label nameLabel = new() { AutoSize = true, Padding = new Padding(15, 10, 4, 0) };
    private readonly CheckBox theme = new() { Appearance = System.Windows.Forms.Appearance.Button, AutoSize = true, Checked = true, MinimumSize = new Size(155, 38), FlatStyle = FlatStyle.Flat };
    private readonly string saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "IronGate", "Valheim", "characters_local");

    public EditorForm()
    {
        Text = "VALHEIM / Редактор персонажа";
        Width = 1180; Height = 820; MinimumSize = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(24, 29, 32); ForeColor = Color.FromArgb(232, 224, 203);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 165, Padding = new Padding(18) };
        var language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        language.Items.AddRange(["Русский", "English"]); language.SelectedIndex = English ? 1 : 0;
        theme.Checked = Dark;
        toolbar.Controls.AddRange([open, export, undoButton, restoreButton, clearFlagsButton, nameLabel, characterName, theme, language]);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 66 };
        bottom.Controls.Add(status);
        foreach (string title in new[] { "Навыки", "Инвентарь" }) tabs.TabPages.Add(new TabPage(title) { BackColor = BackColor, ForeColor = ForeColor });
        tabs.TabPages[0].Controls.Add(skills); tabs.TabPages[1].Controls.Add(inventory);
        for (int i = 0; i < 8; i++) inventory.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));
        for (int i = 0; i < 4; i++) inventory.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        characterName.TextChanged += (_, _) =>
        {
            if (save == null || refreshing) return;
            var field = save.Fields[0]; string previous = field.Value;
            Remember(() => field.Value = previous);
            field.Value = characterName.Text; dirty = true;
        };
        Controls.Add(tabs); Controls.Add(toolbar); Controls.Add(bottom);
        open.Click += (_, _) => Guard(Open);
        export.Click += (_, _) => Guard(Export);
        undoButton.Click += (_, _) => Guard(Undo);
        restoreButton.Click += (_, _) => Guard(Restore);
        clearFlagsButton.Click += (_, _) => Guard(ClearItemFlags);
        FormClosing += (_, e) => { if (!CanDiscard()) e.Cancel = true; };
        theme.CheckedChanged += (_, _) => { Dark = theme.Checked; RefreshInterface(); };
        language.SelectedIndexChanged += (_, _) => { English = language.SelectedIndex == 1; RefreshInterface(); };
        RefreshInterface();
    }

    internal static Button MakeButton(string text) => new() { Text = text, AutoSize = true, MinimumSize = new Size(160, 40), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(47, 58, 61), ForeColor = Color.FromArgb(238, 214, 157), Margin = new Padding(5), Padding = new Padding(5) };
    private bool CanDiscard() => !dirty || MessageBox.Show(T("Отменить несохранённые изменения?", "Discard unsaved changes?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    private void Open()
    {
        if (!CanDiscard()) return;
        using var dialog = new OpenFileDialog { Filter = "Valheim character (*.fch)|*.fch", InitialDirectory = saveFolder };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        var loaded = new SaveFile(File.ReadAllBytes(dialog.FileName));
        if (!loaded.Build().SequenceEqual(loaded.Original)) throw new InvalidDataException("Unchanged round-trip check failed. Editing disabled.");
        save = loaded; source = Path.GetFullPath(dialog.FileName);
        undo.Clear();
        RefreshInterface();
        dirty = false;
    }

    private void RefreshInterface()
    {
        refreshing = true;
        try
        {
        Text = T("VALHEIM / Редактор персонажа", "VALHEIM / Character editor");
        open.Text = T("Открыть персонажа", "Open character");
        export.Text = T("Сохранить в игру", "Save to game");
        undoButton.Text = T("Отменить · Ctrl+Z", "Undo · Ctrl+Z"); undoButton.Enabled = undo.Count > 0;
        restoreButton.Text = T("Восстановить копию", "Restore backup");
        clearFlagsButton.Text = T("Очистить чит-флаги", "Clear item cheat flags");
        clearFlagsButton.Enabled = save?.Items.Any(i => i.Cheated) == true;
        nameLabel.Text = T("Имя персонажа", "Character name");
        theme.Text = Dark ? T("Тема: тёмная", "Theme: dark") : T("Тема: светлая", "Theme: light");
        tabs.TabPages[0].Text = T("Навыки", "Skills"); tabs.TabPages[1].Text = T("Инвентарь", "Inventory");
        status.Text = T("Сохранение в characters_local с резервной копией. Перед сохранением закройте игру.", "Saves to characters_local with a backup. Close the game before saving.");
        if (save == null) { Appearance.Apply(this); return; }
        characterName.Text = save.Fields[0].Value;
        status.Text = save.Name + " / " + status.Text;
        skills.Controls.Clear();
        foreach (var field in save.Fields.Where(f => f.Section == "Skills"))
        {
            var row = new FlowLayoutPanel { Width = 850, Height = 70, BackColor = Color.FromArgb(35, 42, 45), Padding = new Padding(12), Margin = new Padding(0, 0, 0, 8) };
            row.Controls.Add(new Label { Text = SkillLabel(field.Name), Width = 220, Height = 35, TextAlign = ContentAlignment.MiddleLeft });
            var range = new RangeInput(0, 100, decimal.Parse(field.Value, System.Globalization.CultureInfo.InvariantCulture), 2) { Width = 550 };
            range.Changed += value =>
            {
                string previous = field.Value;
                Remember(() => field.Value = previous);
                field.Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture); dirty = true;
            };
            row.Controls.Add(range); skills.Controls.Add(row);
        }
        RenderInventory();
        Appearance.Apply(this);
        }
        finally { refreshing = false; }
    }

    private void Export()
    {
        if (save == null) throw new InvalidOperationException(T("Сначала откройте персонажа.", "Open a character first."));
        ValidateChildren();
        if (Process.GetProcessesByName("valheim").Length != 0) throw new InvalidOperationException(T("Перед сохранением закройте Valheim.", "Close Valheim before saving."));
        byte[] output = save.Build();
        Directory.CreateDirectory(saveFolder);
        string target = Path.Combine(saveFolder, Path.GetFileName(source));
        bool same = string.Equals(target, source, StringComparison.OrdinalIgnoreCase);
        if (!same && File.Exists(target)) throw new IOException(T("В папке игры уже есть такой файл. Откройте именно его, чтобы избежать потери изменений.", "That filename already exists in the game folder. Open it directly to avoid losing changes."));
        if (MessageBox.Show(T("Сохранить персонажа в игру? Предыдущий файл будет сохранён в Backups.", "Save character to the game? The previous file will be retained in Backups.") + "\n\n" + target, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        if (Process.GetProcessesByName("valheim").Length != 0) throw new IOException(T("Закройте Valheim.", "Close Valheim."));
        string backup = SaveStorage.Write(target, output, same ? save.Original : null, Path.Combine(saveFolder, "Backups"));
        save = new SaveFile(output); source = target;
        undo.Clear();
        RefreshInterface();
        dirty = false;
        MessageBox.Show(T("Сохранено:", "Saved:") + "\n" + target + (backup.Length > 0 ? "\n\nBackup:\n" + backup : ""), Text);
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, T("Не удалось выполнить операцию", "Cannot complete operation"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void RenderInventory()
    {
        inventory.Controls.Clear();
        if (save == null) return;
        for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++)
        {
            int column = x, row = y;
            var item = save.Items.Find(i => i.X == x && i.Y == y);
            var definition = item == null ? null : Catalog.Find(item.Hash);
            var cell = MakeButton(item == null ? $"{x + 1}:{y + 1}\n\n+ {T("Добавить", "Add")}" : $"{x + 1}:{y + 1}\n{definition?.DisplayName ?? $"ID {item.Hash}"}\n×{item.Quantity?.Value}  /  {T("ур.", "lvl")} {item.Quality?.Value}");
            cell.AutoSize = false; cell.MinimumSize = Size.Empty; cell.Dock = DockStyle.Fill;
            cell.Image = Catalog.ImageFor(definition);
            cell.ImageAlign = ContentAlignment.TopCenter;
            cell.TextAlign = ContentAlignment.BottomCenter;
            cell.TextImageRelation = TextImageRelation.ImageAboveText;
            cell.AllowDrop = true;
            Point dragStart = Point.Empty;
            bool dragging = false;
            cell.MouseDown += (_, e) => { dragStart = e.Location; dragging = false; };
            cell.MouseMove += (_, e) =>
            {
                if (item == null || e.Button != MouseButtons.Left || dragging) return;
                var threshold = new Rectangle(dragStart.X - SystemInformation.DragSize.Width / 2, dragStart.Y - SystemInformation.DragSize.Height / 2, SystemInformation.DragSize.Width, SystemInformation.DragSize.Height);
                if (threshold.Contains(e.Location)) return;
                dragging = true;
                cell.DoDragDrop(item, DragDropEffects.Move);
            };
            cell.DragEnter += (_, e) => e.Effect = e.Data?.GetData(typeof(InventoryItem)) is InventoryItem dragged && save.Items.Contains(dragged) ? DragDropEffects.Move : DragDropEffects.None;
            cell.DragDrop += (_, e) => Guard(() =>
            {
                if (e.Data?.GetData(typeof(InventoryItem)) is not InventoryItem dragged || !save.Items.Contains(dragged) || (dragged.X == column && dragged.Y == row)) return;
                int oldX = dragged.X, oldY = dragged.Y;
                Remember(() => save.MoveItem(column, row, oldX, oldY));
                save.MoveItem(oldX, oldY, column, row);
                dirty = true; RenderInventory();
            });
            cell.BackColor = item == null ? Color.FromArgb(29, 36, 39) : Color.FromArgb(48, 57, 58);
            cell.Click += (_, _) => Guard(() =>
            {
                if (dragging) { dragging = false; return; }
                using var picker = new ItemPicker(item);
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                var previous = save.Items.ToArray();
                string? quantity = item?.Quantity?.Value, quality = item?.Quality?.Value;
                Remember(() =>
                {
                    save.Items.Clear(); save.Items.AddRange(previous);
                    if (item?.Quantity != null && quantity != null) item.Quantity.Value = quantity;
                    if (item?.Quality != null && quality != null) item.Quality.Value = quality;
                });
                if (picker.Delete) save.Items.RemoveAll(i => i.X == column && i.Y == row);
                else save.SetItem(column, row, picker.Selection!, picker.Quantity, picker.Quality);
                dirty = true; RenderInventory();
            });
            inventory.Controls.Add(cell, x, y);
        }
        Appearance.Apply(inventory);
        clearFlagsButton.Enabled = save.Items.Any(i => i.Cheated);
    }

    private void ClearItemFlags()
    {
        if (save == null) return;
        var flagged = save.Items.Where(i => i.Cheated).ToArray();
        if (flagged.Length == 0) return;
        Remember(() => { foreach (var item in flagged) item.Cheated = true; });
        foreach (var item in flagged) item.Cheated = false;
        dirty = true; RefreshInterface();
        status.Text = T("Сняты флаги предметов: ", "Item flags cleared: ") + flagged.Length + T(". Сохраните изменения в игру.", ". Save changes to the game.");
    }

    private void Remember(Action action)
    {
        undo.Push(action); undoButton.Enabled = true;
    }

    private void Undo()
    {
        if (!undo.TryPop(out var action)) return;
        action(); dirty = true; RefreshInterface();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Z)) { Guard(Undo); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void Restore()
    {
        if (save == null) throw new InvalidOperationException(T("Сначала откройте персонажа для восстановления.", "Open the character to restore first."));
        if (!CanDiscard()) return;
        if (Process.GetProcessesByName("valheim").Length != 0) throw new IOException(T("Закройте Valheim.", "Close Valheim."));
        using var dialog = new OpenFileDialog { InitialDirectory = Path.Combine(saveFolder, "Backups"), Filter = "Backup (*.bak;*.old;*.fch)|*.bak;*.old;*.fch", Title = T("Выберите резервную копию персонажа", "Select a character backup") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        byte[] bytes = File.ReadAllBytes(dialog.FileName);
        var restored = new SaveFile(bytes);
        if (restored.PlayerId != save.PlayerId) throw new InvalidDataException(T("Это копия другого персонажа.", "This backup belongs to a different character."));
        string target = Path.Combine(saveFolder, Path.GetFileName(source));
        if (!string.Equals(target, source, StringComparison.OrdinalIgnoreCase)) throw new IOException(T("Откройте персонажа из characters_local.", "Open the character from characters_local."));
        if (MessageBox.Show(T("Восстановить выбранную копию? Текущий файл будет сохранён в Backups. Несохранённые правки будут отменены.", "Restore this backup? The current file will be backed up. Unsaved edits will be discarded.") + "\n\n" + restored.Name + "\n" + dialog.FileName, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        if (Process.GetProcessesByName("valheim").Length != 0) throw new IOException(T("Закройте Valheim.", "Close Valheim."));
        string backup = SaveStorage.Write(target, bytes, save.Original, Path.Combine(saveFolder, "Backups"));
        save = restored; undo.Clear(); RefreshInterface(); dirty = false;
        MessageBox.Show(T("Копия восстановлена. Предыдущий файл:", "Backup restored. Previous file:") + "\n" + backup, Text);
    }

    private static string SkillLabel(string name) => English ? name : name switch
    {
        "Swords" => "Мечи", "Knives" => "Ножи", "Clubs" => "Дубины", "Polearms" => "Древковое оружие", "Spears" => "Копья",
        "Blocking" => "Блокирование", "Axes" => "Топоры", "Bows" => "Луки", "Elemental magic" => "Магия стихий", "Blood magic" => "Магия крови",
        "Unarmed" => "Без оружия", "Pickaxes" => "Кирки", "Wood cutting" => "Рубка деревьев", "Crossbows" => "Арбалеты",
        "Jump" => "Прыжки", "Sneak" => "Скрытность", "Run" => "Бег", "Swim" => "Плавание", "Fishing" => "Рыбалка",
        "Cooking" => "Кулинария", "Farming" => "Земледелие", "Crafting" => "Ремесло", "Dodge" => "Уклонение", "Ride" => "Верховая езда", _ => name
    };
}
