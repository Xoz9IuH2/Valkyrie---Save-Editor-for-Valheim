using System.IO.Compression;
using System.Text;

namespace ValheimEditor;

public sealed class WorldItem
{
    public required string File { get; init; }
    public required int Offset { get; init; }
    public required int Hash { get; init; }
    public required int Stack { get; init; }
    public required int Quality { get; init; }
    public required string Crafter { get; init; }
    public required bool Cheated { get; init; }
    public string Name => Catalog.Find(Hash)?.DisplayName ?? $"ID {Hash}";
}

public sealed class WorldInventory
{
    public required string File { get; init; }
    public required int Offset { get; init; }
    public List<WorldItem> Items { get; init; } = [];
    public int CheatedCount => Items.Count(i => i.Cheated);
    public string Label
    {
        get
        {
            var crafters = Items.Select(i => i.Crafter).Where(s => s.Length > 0).Distinct().ToList();
            if (crafters.Count > 0) return Appearance.T("Надгробие", "Tombstone") + " · " + string.Join(", ", crafters);
            return Appearance.T("Сундук / контейнер", "Chest / container");
        }
    }
}

public sealed class WorldFileScan
{
    public required string Path { get; init; }
    public required byte[] Original { get; init; }
    public byte[]? Patched { get; set; }
    public List<WorldItem> Items { get; } = [];
    public List<WorldInventory> Inventories { get; } = [];
}

/// <summary>Read-only parser for new-format (0.220+) chunked Valheim worlds.
/// Cleans the cheat flag (bit 0 of each item's trailing byte) without changing
/// any record length, so zone .chunk files can be patched in place.</summary>
public sealed class WorldSave
{
    private const int InventoryVersion = 109;
    private const ushort ChunkTag = 0x0029;

    public string Folder { get; }
    public string Name { get; }
    public List<WorldFileScan> ChunkFiles { get; } = [];
    public int Blocks { get; private set; }
    public int MainBlocks { get; private set; }
    public int SkippedAnchors { get; private set; }
    public int UnsupportedCheated { get; private set; }
    public List<WorldItem> Items => ChunkFiles.SelectMany(f => f.Items).ToList();
    public IEnumerable<WorldItem> Cheated => Items.Where(i => i.Cheated);
    public IEnumerable<WorldInventory> Inventories => ChunkFiles.SelectMany(f => f.Inventories);
    public IEnumerable<WorldInventory> CheatedInventories => Inventories.Where(i => i.CheatedCount > 0);

    public static string WorldsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "IronGate", "Valheim", "worlds_local");

    public static List<string> FindWorlds() => Directory.Exists(WorldsFolder)
        ? Directory.EnumerateDirectories(WorldsFolder).Where(HasMainFile).OrderBy(p => p).ToList()
        : [];

    /// <summary>Cheap display name (reads only the small .fwl2 file, no chunk scanning).</summary>
    public static string DisplayName(string folder) =>
        TryReadName(Directory.EnumerateFiles(folder, "_main.*.fwl2").FirstOrDefault()) ?? Path.GetFileName(folder.TrimEnd('\\'));

    private static bool HasMainFile(string folder) => Directory.EnumerateFiles(folder, "_main.*.db2").Any();

    public WorldSave(string folder)
    {
        Folder = folder;
        string main = Directory.EnumerateFiles(folder, "_main.*.db2").OrderBy(p => p).FirstOrDefault()
            ?? throw new InvalidDataException(Appearance.T(
                "Это не мир нового формата: нет файла _main.*.db2. Старые миры (.db) не поддерживаются — откройте мир в актуальной версии игры, чтобы он пересохранился в новом формате.",
                "Not a new-format world: no _main.*.db2 found. Old single-file (.db) worlds are not supported; open the world in the current game version once so it re-saves in the new format."));
        Name = DisplayName(folder);
        foreach (string chunk in Directory.EnumerateFiles(folder, "*.chunk").OrderBy(p => Path.GetFileName(p)))
        {
            byte[] bytes = File.ReadAllBytes(chunk);
            if (bytes.Length < 6 || BitConverter.ToUInt16(bytes, 0) != ChunkTag)
                throw new InvalidDataException(Appearance.T(
                    $"Неожиданный формат файла зоны {Path.GetFileName(chunk)} — возможно, мир создан другой версией игры.",
                    $"Unexpected zone file format in {Path.GetFileName(chunk)}; the world may come from a different game version."));
            var file = new WorldFileScan { Path = chunk, Original = bytes };
            Blocks += ScanBlob(bytes, 6, file);
            ChunkFiles.Add(file);
        }
        ScanMain(main);
    }

    private static string? TryReadName(string? fwl)
    {
        if (fwl == null) return null;
        try
        {
            using var r = new BinaryReader(new MemoryStream(File.ReadAllBytes(fwl)));
            int length = r.ReadInt32();
            if (length <= 0 || length > r.BaseStream.Length - r.BaseStream.Position) return null;
            if (r.ReadInt32() != 0x29) return null;
            string name = r.ReadString();
            return string.IsNullOrWhiteSpace(name) || name.Length > 64 ? null : name;
        }
        catch { return null; }
    }

    private void ScanMain(string main)
    {
        byte[] bytes = File.ReadAllBytes(main);
        if (bytes.Length < 16 || BitConverter.ToInt32(bytes, 0) != 0x29) throw new InvalidDataException("Invalid main world file header.");
        int length = BitConverter.ToInt32(bytes, 12);
        if (length <= 0 || 16 + length > bytes.Length) throw new InvalidDataException("Invalid main world file length.");
        using var input = new MemoryStream(bytes, 16, length);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        var scan = new WorldFileScan { Path = main, Original = bytes };
        MainBlocks = ScanBlob(output.ToArray(), 0, scan);
        Blocks += MainBlocks;
        UnsupportedCheated = scan.Items.Count(i => i.Cheated);
    }

    /// <summary>Walks the blob looking for [int32 109][u16 count][items] inventory blocks.
    /// A block is accepted only when every record decodes cleanly, sits in an 8x4 slot
    /// grid, has a known catalog prefab hash and a trailing byte of 0 or 1.
    /// Returns the number of blocks found.</summary>
    private int ScanBlob(byte[] blob, int start, WorldFileScan into)
    {
        int i = start;
        int found = 0;
        while (i <= blob.Length - 6)
        {
            if (blob[i] == InventoryVersion && blob[i + 1] == 0 && blob[i + 2] == 0 && blob[i + 3] == 0)
            {
                int count = BitConverter.ToUInt16(blob, i + 4);
                if (count is >= 1 and <= 64)
                {
                    int position = i + 6;
                    var block = new List<WorldItem>(count);
                    bool valid = true;
                    for (int j = 0; j < count; j++)
                    {
                        var item = TryDecodeItem(blob, ref position, into.Path);
                        if (item == null || Catalog.Find(item.Hash) == null) { valid = false; break; }
                        block.Add(item);
                    }
                    if (valid)
                    {
                        into.Inventories.Add(new WorldInventory { File = into.Path, Offset = i, Items = block });
                        into.Items.AddRange(block);
                        found++;
                        i = position;
                        continue;
                    }
                    SkippedAnchors++;
                }
            }
            i++;
        }
        return found;
    }

    private static WorldItem? TryDecodeItem(byte[] blob, ref int position, string file)
    {
        int start = position;
        if (position + 8 > blob.Length) return null;
        byte x = blob[position + 4], y = blob[position + 5], flags = blob[position + 7];
        if (x > 7 || y > 7) return null;
        position += 8;
        int quality = 1, stack = 1;
        if ((flags & 4) != 0) { if (position + 2 > blob.Length) return null; quality = BitConverter.ToUInt16(blob, position); position += 2; }
        if ((flags & 8) != 0) { if (position + 2 > blob.Length) return null; stack = BitConverter.ToUInt16(blob, position); position += 2; }
        if ((flags & 16) != 0) position += 4;
        string crafter = "";
        if ((flags & 32) != 0)
        {
            position += 8;
            string? decoded = ReadString(blob, ref position);
            if (decoded == null) return null;
            crafter = decoded;
        }
        if ((flags & 64) == 0) return null;
        if (position + 4 > blob.Length) return null;
        int hash = BitConverter.ToInt32(blob, position);
        position += 4;
        if ((flags & 128) != 0)
        {
            int custom = ReadVarint(blob, ref position);
            if (custom is < 0 or > 64) return null;
            for (int j = 0; j < custom; j++)
            {
                if (ReadString(blob, ref position) == null) return null;
                if (ReadString(blob, ref position) == null) return null;
            }
        }
        if (position >= blob.Length) return null;
        byte trailing = blob[position];
        position++;
        if (trailing > 1) return null;
        return new WorldItem { File = file, Offset = position - 1, Hash = hash, Stack = stack, Quality = quality,
            Crafter = crafter, Cheated = (trailing & 1) != 0 };
    }

    private static int ReadVarint(byte[] blob, ref int position)
    {
        int value = 0, shift = 0;
        while (position < blob.Length)
        {
            byte b = blob[position++];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return value;
            shift += 7;
            if (shift > 35) break;
        }
        return -1;
    }

    private static string? ReadString(byte[] blob, ref int position)
    {
        int length = ReadVarint(blob, ref position);
        if (length is < 0 or > 4096 || position + length > blob.Length) return null;
        string value = Encoding.UTF8.GetString(blob, position, length);
        position += length;
        return value;
    }

    /// <summary>Clears the cheat bit in every validated item, in memory. Because only
    /// bit 0 of existing bytes changes, patched files keep their exact size and layout.</summary>
    public int CleanFlags()
    {
        if (UnsupportedCheated > 0) throw new InvalidDataException(Appearance.T(
            $"В основном файле мира найдено читовых предметов: {UnsupportedCheated}. Их очистка пока не поддерживается.",
            $"The world main file contains {UnsupportedCheated} cheated item(s); cleaning them is not supported yet."));
        int flipped = 0;
        foreach (var file in ChunkFiles)
        {
            var flagged = file.Items.Where(i => i.Cheated).ToList();
            if (flagged.Count == 0) continue;
            byte[] patched = (byte[])file.Original.Clone();
            foreach (var item in flagged) { patched[item.Offset] &= 0xFE; flipped++; }
            file.Patched = patched;
            Verify(file);
        }
        return flipped;
    }

    private void Verify(WorldFileScan file)
    {
        byte[] patched = file.Patched!;
        if (patched.Length != file.Original.Length) throw new InvalidDataException("Patched file length changed.");
        foreach (var item in file.Items.Where(i => i.Cheated))
        {
            if (patched[item.Offset] != (byte)(file.Original[item.Offset] & 0xFE))
                throw new InvalidDataException("Unexpected patched byte outside cheat flags.");
        }
        for (int i = 0; i < patched.Length; i++)
        {
            byte original = file.Original[i], now = patched[i];
            if (now == original) continue;
            if ((now & 0xFE) != (original & 0xFE) || (original & 1) != 1)
                throw new InvalidDataException("Patched file differs beyond cleared cheat bits.");
        }
        var rescan = new WorldFileScan { Path = file.Path, Original = patched };
        ScanBlob(patched, 6, rescan);
        if (rescan.Items.Count != file.Items.Count || rescan.Items.Any(i => i.Cheated))
            throw new InvalidDataException("Re-scan of the patched file does not match the original scan.");
    }

    public IEnumerable<(string Target, byte[] Output, byte[] Expected)> PendingWrites() =>
        ChunkFiles.Where(f => f.Patched != null && !f.Patched.SequenceEqual(f.Original))
            .Select(f => (f.Path, f.Patched!, f.Original));
}
