using System.Security.Cryptography;

namespace ValheimEditor;

internal static class Tests
{
    public static int Run(string source)
    {
        var log = new List<string>();
        try
        {
            byte[] original = File.ReadAllBytes(source);
            var save = new SaveFile(original);
            Check(save.Build().SequenceEqual(original), "Unchanged save is byte-identical", log);
            log.Add($"Character: {save.Name}; versions: {save.ProfileVersion}/{save.PlayerVersion}/{save.InventoryVersion}; fields: {save.Fields.Count}");
            foreach (Field field in save.Fields)
            {
                string value = field.Value;
                field.Value = field.Name == "Name" ? "Test_\u0418\u043c\u044f" : field.Section == "Skills" ? "42.5" : field.Name == "Durability" ? "123.45" : "7";
                var edited = new SaveFile(save.Build());
                Check(edited.Fields[save.Fields.IndexOf(field)].Value == field.Value, $"Edit/reload {field.Section} / {field.Name}", log);
                for (int j = 0; j < save.Fields.Count; j++)
                    if (j != save.Fields.IndexOf(field) && edited.Fields[j].Value != save.Fields[j].Value) throw new Exception("Unrelated field changed.");
                field.Value = value;
            }
            var skill = save.Fields.FirstOrDefault(f => f.Section == "Skills");
            if (skill != null)
            {
                foreach (string bad in new[] { "NaN", "Infinity", "-1", "101", "abc" })
                {
                    skill.Value = bad;
                    bool rejected = false;
                    try { save.Build(); } catch (InvalidDataException) { rejected = true; }
                    Check(rejected, $"Reject skill value {bad}", log);
                }
                skill.Value = skill.Original;
            }
            byte[] corrupt = original.ToArray(); corrupt[10] ^= 1;
            bool badHash = false;
            try { _ = new SaveFile(corrupt); } catch (InvalidDataException) { badHash = true; }
            Check(badHash, "Reject corrupt checksum", log);
            Check(save.Build().SequenceEqual(original), "Restored edits are byte-identical", log);
            var inventorySave = new SaveFile(original);
            var wood = Catalog.Items.First(i => i.Prefab == "Wood");
            var sword = Catalog.Items.First(i => i.Prefab == "SwordIron");
            Check(wood.MaxStack > 1 && sword.MaxQuality > 1, "Game item catalog loaded", log);
            Check(Catalog.ImageFor(wood) != null && Catalog.ImageFor(sword) != null, "Extracted item icons load", log);
            Check(Catalog.Filter("", "Weapons", false).All(i => i.Category == "Weapons" && !i.Internal), "Category and internal-item filter", log);
            Check(Catalog.Filter("Wood", "Resources", false).Any(i => i.Prefab == "Wood"), "Search combines with category filter", log);
            Check(Catalog.Filter("", "All", true).Count() > Catalog.Filter("", "All", false).Count(), "Internal items hidden by default", log);
            var moved = new SaveFile(original);
            var first = moved.Items[0]; var second = moved.Items[1];
            int fx = first.X, fy = first.Y, sx = second.X, sy = second.Y;
            byte[] firstData = first.Encode(), secondData = second.Encode();
            moved.MoveItem(fx, fy, sx, sy);
            var swapped = new SaveFile(moved.Build());
            var firstAfter = swapped.Items.Single(i => i.X == sx && i.Y == sy);
            byte[] normalized = firstAfter.Encode(); normalized[4] = (byte)fx; normalized[5] = (byte)fy;
            Check(normalized.SequenceEqual(firstData), "Dragging preserves all item metadata except coordinates", log);
            Check(swapped.Items.Single(i => i.X == fx && i.Y == fy).Hash == second.Hash, "Occupied slots swap items", log);
            moved.MoveItem(sx, sy, fx, fy);
            Check(moved.Build().SequenceEqual(original), "Undo swap restores exact original bytes", log);
            moved.Items.Remove(second);
            moved.MoveItem(fx, fy, sx, sy);
            Check(!new SaveFile(moved.Build()).Items.Any(i => i.X == fx && i.Y == fy), "Move into empty slot", log);
            inventorySave.Items.RemoveAll(i => i.X == 0 && i.Y == 0);
            var cleared = new SaveFile(inventorySave.Build());
            Check(!cleared.Items.Any(i => i.X == 0 && i.Y == 0), "Clear inventory slot", log);
            inventorySave.SetItem(0, 0, wood, wood.MaxStack, 1);
            inventorySave.SetItem(0, 0, wood, 2, 1);
            var added = new SaveFile(inventorySave.Build()).Items.Single(i => i.X == 0 && i.Y == 0);
            Check(!added.Cheated, "New items have no cheat flag", log);
            Check(added.Hash == wood.Hash && added.Quantity?.Value == "2", "Add item and edit again before export", log);
            inventorySave.SetItem(0, 0, sword, 1, sword.MaxQuality);
            var replaced = new SaveFile(inventorySave.Build()).Items.Single(i => i.X == 0 && i.Y == 0);
            Check(!replaced.Cheated, "Replacement items have no cheat flag", log);
            var flagSave = new SaveFile(original);
            flagSave.Items[0].Cheated = true;
            var flaggedSave = new SaveFile(flagSave.Build());
            Check(flaggedSave.Items[0].Cheated, "Cheat flag survives serialization", log);
            byte[] flaggedBytes = flaggedSave.Items[0].Encode().ToArray();
            flaggedSave.Items[0].Cheated = false;
            var cleanedSave = new SaveFile(flaggedSave.Build());
            flaggedBytes[^1] &= 254;
            Check(!cleanedSave.Items[0].Cheated && cleanedSave.Items[0].Encode().SequenceEqual(flaggedBytes), "Clearing flag preserves all other item bytes", log);
            flaggedSave.Items[0].Cheated = true;
            Check(flaggedSave.Build().SequenceEqual(flagSave.Build()), "Undo flag clearing restores exact save bytes", log);
            Check(replaced.Hash == sword.Hash && replaced.Quality?.Value == sword.MaxQuality.ToString(), "Replace material with upgraded weapon", log);
            foreach (var limits in new[] { (wood, wood.MaxStack + 1, 1), (sword, 1, sword.MaxQuality + 1), (wood, 0, 1), (sword, 1, 0) })
            {
                bool rejected = false;
                try { inventorySave.SetItem(0, 0, limits.Item1, limits.Item2, limits.Item3); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Reject out-of-range stack/quality", log);
            }
            using (var range = new RangeInput(1, wood.MaxStack, 999999))
                Check(range.Value == wood.MaxStack, "Numeric control clamps to item limit", log);
            using (var form = new EditorForm()) { form.CreateControl(); Check(!form.IsDisposed, "Main form construction", log); }
            using (var form = new EditorForm())
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(EditorForm).GetField("save", flags)!.SetValue(form, new SaveFile(original));
                typeof(EditorForm).GetMethod("RefreshInterface", flags)!.Invoke(form, null);
                var name = (TextBox)typeof(EditorForm).GetField("characterName", flags)!.GetValue(form)!;
                string before = name.Text;
                name.Text = "UndoTest";
                typeof(EditorForm).GetMethod("Undo", flags)!.Invoke(form, null);
                Check(name.Text == before, "Undo restores character name in model and interface", log);
            }
            using (var picker = new ItemPicker(replaced)) { picker.CreateControl(); Check(picker.Selection?.Hash == sword.Hash, "Picker selects existing item", log); }
            foreach (bool english in new[] { false, true }) foreach (bool dark in new[] { false, true })
            {
                Appearance.English = english; Appearance.Dark = dark;
                using var form = new EditorForm();
                using var picker = new ItemPicker(replaced);
                Check(form.BackColor == picker.BackColor, $"Consistent theme / language {dark}/{english}", log);
                Check(wood.DisplayName == (english ? wood.EnglishName : wood.Name), "Localized item name", log);
            }
            string temp = Path.Combine(AppContext.BaseDirectory, "storage-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                string target = Path.Combine(temp, "test.fch"), backups = Path.Combine(temp, "Backups");
                SaveStorage.Write(target, original, null, backups);
                byte[] changed = inventorySave.Build();
                string backup = SaveStorage.Write(target, changed, original, backups);
                Check(File.ReadAllBytes(backup).SequenceEqual(original), "Atomic replacement retains original backup", log);
                Check(File.ReadAllBytes(target).SequenceEqual(changed), "Direct save writes edited bytes", log);
                bool conflict = false;
                try { SaveStorage.Write(target, original, original, backups); } catch (IOException) { conflict = true; }
                Check(conflict && File.ReadAllBytes(target).SequenceEqual(changed), "Stale save rejected without overwriting", log);
                SaveStorage.Write(target, original, changed, backups);
                Check(File.ReadAllBytes(target).SequenceEqual(original), "Repeated save uses latest baseline", log);
            }
            finally { Directory.Delete(temp, true); }
            Check(SHA512.HashData(File.ReadAllBytes(source)).SequenceEqual(SHA512.HashData(original)), "Source file untouched", log);
            log.Add("PASS");
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "test-results.txt"), log);
            return 0;
        }
        catch (Exception ex)
        {
            log.Add("FAIL: " + ex);
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "test-results.txt"), log);
            return 1;
        }
    }

    private static void Check(bool success, string message, List<string> log)
    {
        if (!success) throw new Exception(message);
        log.Add("OK: " + message);
    }
}
