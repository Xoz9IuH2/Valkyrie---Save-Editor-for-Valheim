using System.Diagnostics;
using static ValheimEditor.Appearance;

namespace ValheimEditor;

public sealed class WorldTab : TableLayoutPanel
{
    private readonly ComboBox worlds = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat };
    private readonly Button browse = EditorForm.MakeButton("");
    private readonly Button scan = EditorForm.MakeButton("");
    private readonly Button clean = EditorForm.MakeButton("");
    private readonly Button save = EditorForm.MakeButton("");
    private readonly Button restore = EditorForm.MakeButton("");
    private readonly Label stats = new() { Dock = DockStyle.Fill, Padding = new Padding(8), AutoSize = false };
    private readonly ListView report = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable, HideSelection = false
    };
    public bool Dirty { get; private set; }
    private WorldSave? world;
    private readonly string worldsFolder = WorldSave.WorldsFolder;
    private readonly List<string> folders = [];

    public WorldTab()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(16);
        ColumnCount = 1;
        RowCount = 4;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var picker = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0) };
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        picker.Controls.Add(worlds, 0, 0);
        picker.Controls.Add(browse, 1, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        actions.Controls.AddRange([scan, clean, save, restore]);

        report.Columns.AddRange([
            new ColumnHeader { Width = 280 },
            new ColumnHeader { Width = 90 },
            new ColumnHeader { Width = 110 },
            new ColumnHeader { Width = 280 },
            new ColumnHeader { Width = 220 }]);

        Controls.Add(picker, 0, 0);
        Controls.Add(actions, 0, 1);
        Controls.Add(stats, 0, 2);
        Controls.Add(report, 0, 3);

        FillWorldList();
        worlds.SelectedIndexChanged += (_, _) => UpdateButtons();
        browse.Click += (_, _) => BrowseWorld();
        scan.Click += (_, _) => Guard(ScanWorld);
        clean.Click += (_, _) => Guard(CleanFlags);
        save.Click += (_, _) => Guard(SaveWorld);
        restore.Click += (_, _) => Guard(RestoreBackup);
        UpdateButtons();
    }

    private void FillWorldList()
    {
        folders.Clear(); worlds.Items.Clear();
        foreach (string folder in WorldSave.FindWorlds())
        {
            folders.Add(folder);
            worlds.Items.Add($"{WorldSave.DisplayName(folder)}  ({Path.GetFileName(folder.TrimEnd('\\'))})");
        }
        worlds.Enabled = folders.Count > 0;
        worlds.SelectedIndex = folders.Count > 0 ? 0 : -1;
    }

    private string? SelectedFolder() => worlds.SelectedIndex >= 0 && worlds.SelectedIndex < folders.Count ? folders[worlds.SelectedIndex] : null;

    private void BrowseWorld()
    {
        using var dialog = new FolderBrowserDialog { Description = T("Выберите папку мира", "Select a world folder"), SelectedPath = worldsFolder };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        LoadWorld(dialog.SelectedPath);
    }

    private void ScanWorld()
    {
        string folder = SelectedFolder() ?? throw new InvalidOperationException(T("Сначала выберите мир в списке или нажмите «Обзор».", "Select a world from the list or click Browse first."));
        LoadWorld(folder);
    }

    private void LoadWorld(string folder)
    {
        world = new WorldSave(folder);
        Dirty = false;
        RenderReport();
    }

    private void CleanFlags()
    {
        if (world == null) return;
        int flipped = world.CleanFlags();
        Dirty = true;
        RenderReport();
        stats.Text = T("Сняты чит-флаги: ", "Cleared cheat flags: ") + flipped
            + T(". Нажмите «Сохранить мир», чтобы записать изменения.", ". Click Save world to write the changes.");
    }

    private void SaveWorld()
    {
        if (world == null || !Dirty) return;
        if (Process.GetProcessesByName("valheim").Length != 0) throw new IOException(T("Перед сохранением закройте Valheim.", "Close Valheim before saving."));
        var writes = world.PendingWrites().ToList();
        if (writes.Count == 0) { Dirty = false; return; }
        string backupFolder = WorldStorage.WorldBackupFolder(worldsFolder, Path.GetFileName(world.Folder.TrimEnd('\\')));
        if (MessageBox.Show(T("Сохранить мир в игру? Изменённые файлы будут сохранены с резервными копиями.", "Save the world to the game? Modified files will be saved with backups.")
            + "\n\n" + string.Join("\n", writes.Select(w => Path.GetFileName(w.Target))), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        if (Process.GetProcessesByName("valheim").Length != 0) throw new IOException(T("Закройте Valheim.", "Close Valheim."));
        var backups = WorldStorage.Write(writes, backupFolder);
        world = new WorldSave(world.Folder); Dirty = false;
        RenderReport();
        MessageBox.Show(T("Сохранено:", "Saved:") + "\n" + string.Join("\n", backups), Text);
    }

    private void RestoreBackup()
    {
        if (world == null) return;
        if (Process.GetProcessesByName("valheim").Length != 0) throw new IOException(T("Закройте Valheim.", "Close Valheim."));
        string backupFolder = WorldStorage.WorldBackupFolder(worldsFolder, Path.GetFileName(world.Folder.TrimEnd('\\')));
        if (!Directory.Exists(backupFolder)) throw new IOException(T("Резервных копий нет.", "No backups found."));
        using var dialog = new OpenFileDialog { InitialDirectory = backupFolder, Filter = "Backup (*.bak)|*.bak", Title = T("Выберите резервную копию", "Select a backup file") };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        string name = Path.GetFileName(dialog.FileName);
        int b = name.LastIndexOf('.', name.Length - 5);
        if (b < 0) throw new IOException(T("Неверное имя резервной копии.", "Invalid backup filename."));
        int s = name.LastIndexOf('.', b - 1);
        if (s < 0) throw new IOException(T("Неверное имя резервной копии.", "Invalid backup filename."));
        string originalName = name[..s];
        string target = Path.Combine(world.Folder, originalName);
        if (!File.Exists(target)) throw new IOException(T("Оригинальный файл не найден: ", "Original file not found: ") + originalName);
        byte[] currentBytes = File.ReadAllBytes(target);
        byte[] backupBytes = File.ReadAllBytes(dialog.FileName);
        if (MessageBox.Show(T("Восстановить файл из копии? Текущая версия будет сохранена в резервных копиях.", "Restore file from backup? The current version will be backed up.")
            + "\n\n" + originalName, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        WorldStorage.ReplaceFile(target, backupBytes, currentBytes, backupFolder);
        world = new WorldSave(world.Folder); Dirty = false;
        RenderReport();
        MessageBox.Show(T("Восстановлено:", "Restored:") + "\n" + target, Text);
    }

    private void RenderReport()
    {
        report.Items.Clear();
        if (world == null)
        {
            stats.Text = T("Выберите мир и нажмите «Анализировать мир». Появятся сундуки и предметы с чит-флагом.", "Select a world and click Analyze world. Chests and cheated items will appear here.");
            UpdateButtons();
            return;
        }
        int cheatedContainers = world.CheatedInventories.Count();
        int cheatedItems = world.Cheated.Count();
        stats.Text =
            T("Мир: ", "World: ") + world.Name + "\n" +
            T("Контейнеров: ", "Containers: ") + world.Blocks +
            T("  ·  Предметов: ", "  ·  Items: ") + world.Items.Count +
            T("  ·  Сундуков с читами: ", "  ·  Cheated containers: ") + cheatedContainers +
            T("  ·  Предметов с чит-флагом: ", "  ·  Cheated items: ") + cheatedItems +
            (Dirty ? T("  ·  Флаги сняты, сохраните мир.", "  ·  Flags cleared, save the world.") : "");
        if (world.UnsupportedCheated > 0) stats.Text += "\n" + T("В main-файле мира читовых: ", "Unsupported cheated in main: ") + world.UnsupportedCheated;

        foreach (var inventory in world.CheatedInventories)
        {
            var row = new ListViewItem(inventory.Label);
            row.SubItems.Add(inventory.Items.Count.ToString());
            row.SubItems.Add(inventory.CheatedCount.ToString());
            row.SubItems.Add(string.Join(", ", inventory.Items.Where(i => i.Cheated).Select(i => i.Name + (i.Stack > 1 ? " ×" + i.Stack : "")).Take(6)));
            row.SubItems.Add(Path.GetFileName(inventory.File));
            report.Items.Add(row);
        }
        if (report.Items.Count == 0)
        {
            var row = new ListViewItem(T("Чит-флагов в контейнерах нет", "No cheated items in containers"));
            row.SubItems.Add(world.Blocks.ToString());
            row.SubItems.Add("0");
            row.SubItems.Add("");
            row.SubItems.Add("");
            report.Items.Add(row);
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasWorld = world != null;
        scan.Enabled = SelectedFolder() != null;
        clean.Enabled = hasWorld && world!.Cheated.Any() && !Dirty;
        save.Enabled = Dirty;
        restore.Enabled = hasWorld && Directory.Exists(WorldStorage.WorldBackupFolder(worldsFolder, Path.GetFileName(world!.Folder.TrimEnd('\\'))));
    }

    public void RefreshTexts()
    {
        browse.Text = T("Обзор...", "Browse...");
        scan.Text = T("Анализировать мир", "Analyze world");
        clean.Text = T("Очистить чит-флаги", "Clear cheat flags");
        save.Text = T("Сохранить мир", "Save world");
        restore.Text = T("Восстановить копию", "Restore backup");
        report.Columns[0].Text = T("Контейнер", "Container");
        report.Columns[1].Text = T("Предметов", "Items");
        report.Columns[2].Text = T("С читом", "Cheated");
        report.Columns[3].Text = T("Читовые предметы", "Cheated items");
        report.Columns[4].Text = T("Файл", "File");
        report.BackColor = Surface;
        report.ForeColor = TextColor;
        report.BorderStyle = BorderStyle.None;
        RenderReport();
    }

    private static void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, T("Не удалось выполнить операцию", "Cannot complete operation"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
