using System.IO.Compression;
using System.Security.Cryptography;

namespace ValheimEditor;

internal static class Tests
{
    public static int Run(string source)
    {
        var log = new List<string>();
        try
        {
            if (Directory.Exists(source) && Directory.EnumerateFiles(source, "_main.*.db2").Any()) return RunWorldTests(source, log);
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
            Check(wood.Stats(1, 10).Any(s => s.Label.Contains("Weight") || s.Label.Contains("Вес")), "Wood reports stack weight", log);
            Check(sword.Stats(3, 1).Any(s => s.Value.Contains("+") && s.Value.Contains("=")), "Weapon stats include quality bonus", log);
            var honey = Catalog.Items.First(i => i.Prefab == "Honey");
            Check(honey.Stats(1, 1).Any(s => s.Label.Contains("Health") || s.Label.Contains("Здоровье")), "Food reports health and stamina", log);
            using (var picker = new ItemPicker(null)) { picker.CreateControl(); Check(picker.Controls.OfType<Panel>().Any(p => p.Width == 280), "Item picker has left-hand stats panel", log); }
            var moved = new SaveFile(original);
            bool seededInventory = moved.Items.Count < 2;
            if (seededInventory)
            {
                moved.SetItem(0, 0, wood, 1, 1);
                moved.SetItem(1, 0, sword, 1, 1);
                log.Add("OK: Seed two items for move tests on sparse inventory");
            }
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
            if (!seededInventory)
                Check(moved.Build().SequenceEqual(original), "Undo swap restores exact original bytes", log);
            else
                log.Add("SKIP: Undo swap byte-compare on seeded sparse inventory");
            moved.Items.Remove(second);
            moved.MoveItem(fx, fy, sx, sy);
            Check(!new SaveFile(moved.Build()).Items.Any(i => i.X == fx && i.Y == fy), "Move into empty slot", log);
            inventorySave.Items.RemoveAll(i => i.X == 0 && i.Y == 0);
            var cleared = new SaveFile(inventorySave.Build());
            Check(!cleared.Items.Any(i => i.X == 0 && i.Y == 0), "Clear inventory slot", log);
            if (inventorySave.Items.Count == 0)
            {
                inventorySave.SetItem(0, 0, wood, 2, 1);
                log.Add("OK: Seed item for empty-inventory saves");
            }
            inventorySave.SetItem(0, 0, wood, wood.MaxStack, 1);
            inventorySave.SetItem(0, 0, wood, 2, 1);
            var added = new SaveFile(inventorySave.Build()).Items.Single(i => i.X == 0 && i.Y == 0);
            Check(!added.Cheated, "New items have no cheat flag", log);
            Check(added.Hash == wood.Hash && added.Quantity?.Value == "2", "Add item and edit again before export", log);
            inventorySave.SetItem(0, 0, sword, 1, sword.MaxQuality);
            var replaced = new SaveFile(inventorySave.Build()).Items.Single(i => i.X == 0 && i.Y == 0);
            Check(!replaced.Cheated, "Replacement items have no cheat flag", log);
            var flagSave = new SaveFile(inventorySave.Build());
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
            {
                var copySave = new SaveFile(original);
                if (copySave.Items.Count == 0) copySave.SetItem(0, 0, sword, 1, sword.MaxQuality);
                var sourceItem = copySave.Items[0];
                int emptyX = Enumerable.Range(0, 8).SelectMany(x => Enumerable.Range(0, copySave.InventoryRows).Select(y => (x, y))).First(p => !copySave.Items.Any(i => i.X == p.x && i.Y == p.y)).x;
                int emptyY = Enumerable.Range(0, copySave.InventoryRows).First(y => !copySave.Items.Any(i => i.X == emptyX && i.Y == y));
                byte[] snapshot = sourceItem.Snapshot();
                copySave.PasteItem(emptyX, emptyY, snapshot);
                var pasted = new SaveFile(copySave.Build()).Items.Single(i => i.X == emptyX && i.Y == emptyY);
                byte[] expected = snapshot.ToArray(); expected[4] = (byte)emptyX; expected[5] = (byte)emptyY;
                Check(pasted.Snapshot().SequenceEqual(expected), "Ctrl+C/Ctrl+V paste preserves item bytes except slot", log);
                Check(copySave.Items.Count(i => i.Hash == sourceItem.Hash) >= 2, "Paste duplicates instead of moving", log);
            }
            foreach (var limits in new[] { (wood, wood.MaxStack + 1, 1), (sword, 1, sword.MaxQuality + 1), (wood, 0, 1), (sword, 1, 0) })
            {
                bool rejected = false;
                try { inventorySave.SetItem(0, 0, limits.Item1, limits.Item2, limits.Item3); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Reject out-of-range stack/quality", log);
            }
            using (var range = new RangeInput(1, wood.MaxStack, 999999))
                Check(range.Value == wood.MaxStack, "Numeric control clamps to item limit", log);
            // Quality modes: high quality survives round-trip regardless of mode; gates only apply to SetItem.
            {
                var high = new SaveFile(original);
                QualityMode.Level = QualityLevel.Sensible;
                high.SetItem(0, 0, wood, 1, 5);
                QualityMode.Level = QualityLevel.Catalog;
                var reloadedAny = new SaveFile(high.Build());
                Check(reloadedAny.Items.Single(i => i.X == 0 && i.Y == 0).Quality!.Value == "5", "Quality 5 above catalog maximum survives round-trip", log);
                QualityMode.Level = QualityLevel.Sensible;
                Check(QualityMode.MaxQuality(sword) == QualityMode.SensibleMax, "Sensible mode raises the quality limit to 99", log);
                high.SetItem(0, 0, sword, 1, 99);
                var q99 = new SaveFile(high.Build()).Items.Single(i => i.X == 0 && i.Y == 0);
                Check(q99.Quality?.Value == "99", "Sensible mode allows quality 99", log);
                QualityMode.Level = QualityLevel.Lunatic;
                high.SetItem(0, 0, sword, 1, 999);
                var q999 = new SaveFile(high.Build()).Items.Single(i => i.X == 0 && i.Y == 0);
                Check(q999.Quality?.Value == "999", "Lunatic mode allows quality 999", log);
                bool rejected = false;
                try { high.SetItem(0, 0, sword, 1, QualityMode.FormatMax + 1); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Quality above the format field width is rejected", log);
                try { high.SetItem(0, 0, sword, 1, QualityMode.SensibleMax + 1); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Sensible mode rejects quality 100", log);
                QualityMode.Level = QualityLevel.Catalog;
                try { high.SetItem(0, 0, sword, 1, sword.MaxQuality + 1); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Catalog mode rejects quality above the catalog maximum", log);
                using (var picker = new ItemPicker(null)) { picker.CreateControl(); Check(picker.Quality == 1, "Picker default quality", log); }
                Check(high.Build() != null, "Quality mode edits build", log);
            }
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
            RunSyntheticWorldTests(log);
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

    private static int RunWorldTests(string worldFolder, List<string> log)
    {
        try
        {
            string temp = Path.Combine(AppContext.BaseDirectory, "world-test-" + Guid.NewGuid().ToString("N"));
            CopyDirectory(worldFolder, temp);
            try
            {
                var world = new WorldSave(temp);
                int cheated = world.Cheated.Count();
                log.Add($"World: {world.Name}; chunks: {world.ChunkFiles.Count}; inventories: {world.Blocks}; items: {world.Items.Count}; cheated: {cheated}; skipped anchors: {world.SkippedAnchors}");
                Check(world.ChunkFiles.Count > 0, "World chunk files discovered", log);
                Check(world.Blocks > 0 && world.Items.Count > 0, "Container inventories parsed", log);
                Check(world.UnsupportedCheated == 0, "No cheated items in unsupported locations", log);
                byte[][] originals = world.ChunkFiles.Select(f => f.Original).ToArray();
                int flipped = world.CleanFlags();
                Check(flipped == cheated, "CleanFlags flips exactly the cheated items", log);
                var writes = world.PendingWrites().ToList();
                Check(writes.Count == world.ChunkFiles.Count(f => f.Items.Any(i => i.Cheated)), "Only files containing cheated items are staged", log);
                foreach (var file in world.ChunkFiles.Where(f => f.Patched != null))
                {
                    Check(file.Patched!.Length == file.Original.Length, $"Patched length unchanged: {Path.GetFileName(file.Path)}", log);
                    var diff = Enumerable.Range(0, file.Patched.Length).Where(i => file.Patched[i] != file.Original[i]).ToList();
                    var expected = file.Items.Where(i => i.Cheated).Select(i => i.Offset).OrderBy(o => o).ToList();
                    Check(diff.SequenceEqual(expected), "Byte differences are exactly the cheated flag offsets", log);
                    Check(diff.All(o => (file.Original[o] & 1) == 1 && file.Patched[o] == (byte)(file.Original[o] & 0xFE)), "Each difference only clears bit 0", log);
                }
                string backupFolder = Path.Combine(temp, "..", "backups-test");
                var backups = WorldStorage.Write(writes, backupFolder);
                Check(backups.Count == writes.Count && backups.All(File.Exists), "Atomic multi-file write with backups", log);
                Check(world.ChunkFiles.All(f => f.Patched == null || File.ReadAllBytes(f.Path).SequenceEqual(f.Patched)), "Patched bytes written to disk", log);
                var rescanned = new WorldSave(temp);
                Check(rescanned.Cheated.Count() == 0 && rescanned.Blocks == world.Blocks && rescanned.Items.Count == world.Items.Count, "Re-scanned world is clean and structurally identical", log);
                if (backups.Count > 0)
                {
                    string target = writes[0].Target;
                    WorldStorage.ReplaceFile(target, writes[0].Expected, writes[0].Output, backupFolder);
                    Check(File.ReadAllBytes(target).SequenceEqual(writes[0].Expected), "Backup restore returns original bytes", log);
                    Check(new WorldSave(temp).Cheated.Count() == cheated, "Restore brings the cheat flags back", log);
                }
                Check(!Directory.EnumerateFiles(temp).Any(f => Path.GetFileName(f).StartsWith(".editor-")), "No temporary files left behind", log);
                string sourceWorld = worldFolder.TrimEnd('\\');
                Check(SHA512.HashData(File.ReadAllBytes(Directory.EnumerateFiles(sourceWorld, "_main.*.db2").First())).SequenceEqual(
                      SHA512.HashData(File.ReadAllBytes(Directory.EnumerateFiles(temp, "_main.*.db2").First()))), "Source world untouched", log);
            }
            finally { Directory.Delete(temp, true); }
            RunSyntheticWorldTests(log);
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

    private static void RunSyntheticWorldTests(List<string> log)
    {
        string temp = Path.Combine(AppContext.BaseDirectory, "synthetic-world-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            int wood = Catalog.Items.First(i => i.Prefab == "Wood").Hash;
            int sword = Catalog.Items.First(i => i.Prefab == "SwordIron").Hash;
            byte[] cheated = ItemBytes(wood, cheatedByte: 1, stack: 50);
            byte[] crafted = ItemBytes(sword, cheatedByte: 0, x: 1, quality: 2, crafter: "Йода", custom: [("hit", "1")]);
            byte[] clean = ItemBytes(wood, cheatedByte: 0, stack: 15);
            File.WriteAllBytes(Path.Combine(temp, "10_10__1_1.chunk"), ChunkBytes(InventoryBytes(cheated, crafted), InventoryBytes(clean)));
            WriteMainFile(Path.Combine(temp, "_main.1.db2"), []);
            var world = new WorldSave(temp);
            Check(world.Blocks == 2 && world.Items.Count == 3, "Synthetic inventories discovered", log);
            Check(world.Inventories.Count() == 2 && world.CheatedInventories.Count() == 1, "Inventories grouped with cheat counts", log);
            Check(world.Cheated.Count() == 1 && world.Cheated.First().Offset > 0, "Cheat flag located", log);
            var craftedItem = world.Items.Single(i => i.Hash == sword);
            Check(craftedItem.Crafter == "Йода" && craftedItem.Quality == 2, "Crafter name and quality decoded", log);
            Check(world.Items.All(i => i.Stack is 50 or 1 or 15), "Stack sizes decoded", log);
            int flipped = world.CleanFlags();
            Check(flipped == 1, "One bit flipped in synthetic world", log);
            var patched = world.ChunkFiles[0].Patched!;
            var diff = Enumerable.Range(0, patched.Length).Where(i => patched[i] != world.ChunkFiles[0].Original[i]).ToList();
            var cheatedOffset = world.Items.Where(i => i.Cheated).Single().Offset;
            Check(diff.Count == 1 && diff[0] == cheatedOffset && patched[cheatedOffset] == 0, "Exactly the cheat byte changed to zero", log);

            File.WriteAllBytes(Path.Combine(temp, "11_11__1_1.chunk"), ChunkBytes(InventoryBytes(ItemBytes(int.MaxValue, 1))));
            var unknown = new WorldSave(temp);
            Check(unknown.Items.Count == 3 && unknown.Items.All(i => Path.GetFileName(i.File) == "10_10__1_1.chunk"), "Inventory with unknown prefab hash is skipped", log);

            var random = new Random(1234);
            byte[] noise = new byte[1_000_000];
            random.NextBytes(noise);
            File.WriteAllBytes(Path.Combine(temp, "12_12__1_1.chunk"), ChunkBytes());
            File.WriteAllBytes(Path.Combine(temp, "12_12__1_1.chunk"), ChunkBytes().Take(6).Concat(noise).ToArray());
            var noisy = new WorldSave(temp);
            Check(noisy.ChunkFiles.Single(f => Path.GetFileName(f.Path) == "12_12__1_1.chunk").Items.Count == 0, "Random data produces no false inventories", log);

            File.Delete(Path.Combine(temp, "11_11__1_1.chunk"));
            File.Delete(Path.Combine(temp, "12_12__1_1.chunk"));
            WriteMainFile(Path.Combine(temp, "_main.1.db2"), [InventoryBytes(ItemBytes(wood, cheatedByte: 1, stack: 5))]);
            var unsupported = new WorldSave(temp);
            Check(unsupported.UnsupportedCheated == 1, "Cheated item in main file detected as unsupported", log);
            bool refused = false;
            try { unsupported.CleanFlags(); } catch (InvalidDataException) { refused = true; }
            Check(refused, "Cleaning refuses unsupported main-file items", log);

            File.WriteAllBytes(Path.Combine(temp, "13_13__1_1.chunk"), ChunkBytes(InventoryBytes(ItemBytes(wood, cheatedByte: 2))));
            var oddTrailing = new WorldSave(temp);
            Check(oddTrailing.Items.All(i => Path.GetFileName(i.File) != "13_13__1_1.chunk"), "Unusual trailing byte values are skipped", log);
            using (var form = new EditorForm()) { form.CreateControl(); Check(!form.IsDisposed, "Main form with world tab construction", log); }
        }
        finally { Directory.Delete(temp, true); }
    }

    private static byte[] ItemBytes(int hash, byte cheatedByte, int x = 0, int y = 0, int stack = 1, int quality = 1, string? crafter = null, List<(string Key, string Value)>? custom = null)
    {
        using var buffer = new MemoryStream();
        using var w = new BinaryWriter(buffer);
        w.Write(100000);
        w.Write((byte)x); w.Write((byte)y); w.Write((byte)0);
        byte flags = 64;
        if (quality != 1) flags |= 4;
        if (stack != 1) flags |= 8;
        if (crafter != null) flags |= 32;
        if (custom != null) flags |= 128;
        w.Write(flags);
        if (quality != 1) w.Write((ushort)quality);
        if (stack != 1) w.Write((ushort)stack);
        if (crafter != null) { w.Write(0L); w.Write(crafter); }
        w.Write(hash);
        if (custom != null)
        {
            w.Write((byte)custom.Count);
            foreach (var (key, value) in custom) { w.Write(key); w.Write(value); }
        }
        w.Write(cheatedByte);
        return buffer.ToArray();
    }

    private static byte[] InventoryBytes(params byte[][] items)
    {
        using var buffer = new MemoryStream();
        using var w = new BinaryWriter(buffer);
        w.Write(109);
        w.Write((ushort)items.Length);
        foreach (byte[] item in items) w.Write(item);
        return buffer.ToArray();
    }

    private static byte[] ChunkBytes(params byte[][] inventories)
    {
        using var buffer = new MemoryStream();
        using var w = new BinaryWriter(buffer);
        w.Write((ushort)0x0029);
        w.Write(5);
        w.Write(new byte[37]);
        foreach (byte[] inventory in inventories) w.Write(inventory);
        w.Write(new byte[64]);
        return buffer.ToArray();
    }

    private static void WriteMainFile(string path, byte[][] inventories)
    {
        using var payload = new MemoryStream();
        using (var w = new BinaryWriter(payload))
        {
            w.Write(new byte[64]);
            foreach (byte[] inventory in inventories) w.Write(inventory);
        }
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, true)) gzip.Write(payload.ToArray());
        using var file = new BinaryWriter(File.Create(path));
        file.Write(0x29);
        file.Write(0L);
        file.Write((int)compressed.Length);
        file.Write(compressed.ToArray());
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (string folder in Directory.EnumerateDirectories(source)) CopyDirectory(folder, Path.Combine(target, Path.GetFileName(folder)));
    }

    private static void Check(bool success, string message, List<string> log)
    {
        log.Add((success ? "OK: " : "FAIL: ") + message);
        if (!success) throw new Exception(message);
    }
}
