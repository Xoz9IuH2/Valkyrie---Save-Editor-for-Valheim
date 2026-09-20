using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ValheimEditor;

public sealed class Field
{
    public string Section { get; init; } = "";
    public string Name { get; init; } = "";
    public string Value { get; set; } = "";
    public string Original { get; init; } = "";
    public Func<string, byte[]> Encode { get; init; } = null!;
    public int Start { get; init; }
    public int Length { get; init; }
}

public sealed class SaveFile
{
    public byte[] Original { get; }
    private readonly byte[] payload;
    private int playerLengthOffset;
    private int playerStart;
    public string Name { get; private set; } = "";
    public long PlayerId { get; }
    public int ProfileVersion { get; }
    public int PlayerVersion { get; private set; }
    public int InventoryVersion { get; private set; }
    public int InventoryRows { get; private set; } = 4;
    public int InventorySlots => InventoryRows * 8;
    public List<Field> Fields { get; } = [];
    public List<InventoryItem> Items { get; } = [];
    private readonly List<(int Start, int Length, Func<byte[]> Encode)> regions = [];

    public SaveFile(byte[] bytes)
    {
        Original = bytes.ToArray();
        using var outer = new BinaryReader(new MemoryStream(bytes));
        payload = ReadBlob(outer);
        var hash = ReadBlob(outer);
        if (outer.BaseStream.Position != bytes.Length || !SHA512.HashData(payload).SequenceEqual(hash))
            throw new InvalidDataException("Invalid save length or SHA-512 checksum. Original file was not changed.");
        using var r = new BinaryReader(new MemoryStream(payload), new UTF8Encoding(false, true));
        ProfileVersion = r.ReadInt32();
        if (ProfileVersion != 46)
            throw new InvalidDataException($"Unsupported profile version {ProfileVersion}; expected 46. No changes made.");
        int stats = Count(r), groups = Count(r);
        for (int i = 0; i < groups; i++)
        {
            Skip(r, checked(stats * 4));
            for (int j = 0; j < 3; j++) StringValues(r);
            int enemies = Count(r);
            for (int j = 0; j < enemies; j++) StringValues(r);
            for (int j = 0; j < 5; j++) StringValues(r);
        }
        r.ReadBoolean();
        int worlds = Count(r);
        for (int i = 0; i < worlds; i++)
        {
            Skip(r, 8 + 13 * 3 + 12);
            if (r.ReadBoolean()) ReadBlob(r);
        }
        int nameStart = Pos(r);
        Name = r.ReadString();
        Add("Character", "Name", Name, nameStart, Pos(r) - nameStart, s =>
        {
            if (string.IsNullOrWhiteSpace(s) || s.Length > 64 || s.Any(char.IsControl))
                throw new InvalidDataException("Name must contain 1-64 characters without control characters.");
            return Pack(w => w.Write(s));
        });
        PlayerId = r.ReadInt64();
        r.ReadString();
        r.ReadBoolean();
        r.ReadInt64();
        if (!r.ReadBoolean()) throw new InvalidDataException("This character has no player data yet. Enter a world and save first.");
        playerLengthOffset = Pos(r);
        int playerLength = Count(r, payload.Length);
        playerStart = Pos(r);
        if (playerStart + playerLength != payload.Length) throw new InvalidDataException("Unexpected player data length.");
        PlayerVersion = r.ReadInt32();
        if (PlayerVersion != 33) throw new InvalidDataException($"Unsupported player data version {PlayerVersion}; expected 33.");
        Skip(r, 16);
        r.ReadString();
        Skip(r, 4);
        InventoryVersion = r.ReadInt32();
        if (InventoryVersion != 109) throw new InvalidDataException($"Unsupported inventory version {InventoryVersion}; expected 109.");
        int inventoryStart = Pos(r);
        int items = r.ReadUInt16();
        for (int i = 0; i < items; i++) ReadItem(r, i);
        int inventoryEnd = Pos(r);
        regions.Add((inventoryStart, inventoryEnd - inventoryStart, () => Pack(w =>
        {
            if (Items.Count > InventorySlots || Items.Select(i => (i.X, i.Y)).Distinct().Count() != Items.Count || Items.Any(i => i.X is < 0 or > 7 || i.Y < 0 || i.Y >= InventoryRows))
                throw new InvalidDataException("Invalid inventory slots.");
            w.Write((ushort)Items.Count);
            foreach (var item in Items)
            {
                byte[] encoded = item.Encode().ToArray();
                encoded[4] = (byte)item.X; encoded[5] = (byte)item.Y;
                encoded[^1] = (byte)((encoded[^1] & ~1) | (item.Cheated ? 1 : 0));
                w.Write(encoded);
            }
        })));
        Strings(r);
        StringValues(r);
        for (int i = 0; i < 5; i++)
        {
            var list = ReadStrings(r);
            if (i == 2)
            {
                if (list.Contains("invrows 6") || list.Contains("invslot2")) InventoryRows = Math.Max(InventoryRows, 6);
                else if (list.Contains("invrows 5") || list.Contains("invslot1")) InventoryRows = Math.Max(InventoryRows, 5);
            }
        }
        if (Items.Count > 0)
        {
            int maxY = Items.Max(item => item.Y);
            if (maxY >= 5) InventoryRows = Math.Max(InventoryRows, 6);
            else if (maxY >= 4) InventoryRows = Math.Max(InventoryRows, 5);
            else InventoryRows = Math.Max(InventoryRows, maxY + 1);
        }
        int texts = Count(r);
        for (int i = 0; i < texts; i++) { r.ReadString(); r.ReadString(); }
        r.ReadString();
        r.ReadString();
        Skip(r, 28);
        StringValues(r);
        if (r.ReadInt32() != 2) throw new InvalidDataException("Unsupported skills version.");
        int skills = Count(r);
        for (int i = 0; i < skills; i++)
        {
            int id = r.ReadInt32();
            int start = Pos(r);
            float level = r.ReadSingle();
            Add("Skills", SkillName(id), level.ToString("R", CultureInfo.InvariantCulture), start, 4,
                s => Pack(w => w.Write(Number(s, 0, 100))));
            r.ReadSingle();
        }
        int custom = Count(r);
        for (int i = 0; i < custom; i++) { r.ReadString(); r.ReadString(); }
        Skip(r, 12);
        ReadBlob(r);
        if (Pos(r) != payload.Length) throw new InvalidDataException("Unrecognized trailing player data.");
    }

    private void ReadItem(BinaryReader r, int index)
    {
        int start = Pos(r);
        int durability = r.ReadInt32();
        byte x = r.ReadByte(), y = r.ReadByte(), world = r.ReadByte(), flags = r.ReadByte();
        int quality = (flags & 4) != 0 ? r.ReadUInt16() : 1;
        int stack = (flags & 8) != 0 ? r.ReadUInt16() : 1;
        int restStart = Pos(r);
        if ((flags & 16) != 0) r.ReadInt32();
        if ((flags & 32) != 0) { r.ReadInt64(); r.ReadString(); }
        int hash = (flags & 64) != 0 ? r.ReadInt32() : 0;
        int custom = 0;
        if ((flags & 128) != 0)
        {
            custom = r.ReadByte();
            if ((custom & 128) != 0) custom = ((custom & 127) << 8) | r.ReadByte();
        }
        for (int j = 0; j < custom; j++) { r.ReadString(); r.ReadString(); }
        bool cheated = (r.ReadByte() & 1) != 0;
        int end = Pos(r);
        string section = $"Item {index + 1}: slot {x + 1},{y + 1} / hash {hash}";
        var d = Add(section, "Durability", (durability / 100m).ToString(CultureInfo.InvariantCulture), 0, 0,
            s => Pack(w => w.Write(checked((int)decimal.Round(Decimal(s, 0, 21474836m) * 100)))));
        var q = Add(section, "Quality", quality.ToString(), 0, 0, s => Pack(w => w.Write(Integer(s, 1, 65535))));
        var n = Add(section, "Quantity", stack.ToString(), 0, 0, s => Pack(w => w.Write(Integer(s, 1, 65535))));
        regions.Add((start, end - start, () =>
        {
            int newQuality = Integer(q.Value, 1, 65535), newStack = Integer(n.Value, 1, 65535);
            byte newFlags = (byte)((flags & ~12) | (newQuality != 1 ? 4 : 0) | (newStack != 1 ? 8 : 0));
            return Pack(w =>
            {
                w.Write(d.Value == d.Original ? BitConverter.GetBytes(durability) : d.Encode(d.Value));
                w.Write(x); w.Write(y); w.Write(world); w.Write(newFlags);
                if ((newFlags & 4) != 0) w.Write((ushort)newQuality);
                if ((newFlags & 8) != 0) w.Write((ushort)newStack);
                w.Write(payload, restStart, end - restStart);
            });
        }));
        // Keep unusual but valid optional-field encodings byte-identical unless edited.
        var region = regions[^1];
        regions.RemoveAt(regions.Count - 1);
        Items.Add(new InventoryItem { X = x, Y = y, Hash = hash, Quantity = n, Quality = q, Cheated = cheated,
            Encode = () => d.Value == d.Original && q.Value == q.Original && n.Value == n.Original ? payload[start..end] : region.Encode() });
    }

    public void SetItem(int x, int y, ItemDefinition definition, int quantity, int quality)
    {
        if (x is < 0 or > 7 || y < 0 || y >= InventoryRows || quantity < 1 || quantity > definition.MaxStack || quality < 1 || quality > definition.MaxQuality)
            throw new InvalidDataException("Item exceeds stack, quality or slot limits.");
        var existing = Items.Find(i => i.X == x && i.Y == y);
        if (existing?.Hash == definition.Hash && existing.Quantity?.Original.Length > 0 && existing.Quality != null)
        {
            existing.Quantity.Value = quantity.ToString();
            existing.Quality.Value = quality.ToString();
            return;
        }
        byte[] bytes = Pack(w =>
        {
            w.Write(checked((int)((definition.Durability + (quality - 1) * definition.DurabilityPerLevel) * 100)));
            w.Write((byte)x); w.Write((byte)y); w.Write((byte)0);
            w.Write((byte)(65 | (quality > 1 ? 4 : 0) | (quantity > 1 ? 8 : 0)));
            if (quality > 1) w.Write((ushort)quality);
            if (quantity > 1) w.Write((ushort)quantity);
            w.Write(definition.Hash); w.Write((byte)0);
        });
        Items.RemoveAll(i => i.X == x && i.Y == y);
        Items.Add(new InventoryItem { X = x, Y = y, Hash = definition.Hash, Encode = () => bytes,
            Quantity = new Field { Value = quantity.ToString() }, Quality = new Field { Value = quality.ToString() } });
    }

    public void MoveItem(int x, int y, int toX, int toY)
    {
        if (toX is < 0 or > 7 || toY < 0 || toY >= InventoryRows) throw new InvalidDataException("Invalid destination slot.");
        var item = Items.SingleOrDefault(i => i.X == x && i.Y == y);
        if (item == null) return;
        var other = Items.SingleOrDefault(i => i.X == toX && i.Y == toY);
        if (other != null) { other.X = x; other.Y = y; }
        item.X = toX; item.Y = toY;
    }

    private Field Add(string section, string name, string value, int start, int length, Func<string, byte[]> encode)
    {
        var field = new Field { Section = section, Name = name, Value = value, Original = value, Start = start, Length = length, Encode = encode };
        Fields.Add(field);
        if (length > 0) regions.Add((start, length, () => field.Value == field.Original ? payload[start..(start + length)] : field.Encode(field.Value)));
        return field;
    }

    public byte[] Build()
    {
        var changes = regions.Select(p => (p.Start, p.Length, Data: p.Encode())).OrderBy(p => p.Start).ToList();
        int delta = changes.Where(p => p.Start >= playerStart).Sum(p => p.Data.Length - p.Length);
        changes.Add((playerLengthOffset, 4, BitConverter.GetBytes(payload.Length - playerStart + delta)));
        using var stream = new MemoryStream();
        int position = 0;
        foreach (var change in changes.OrderBy(p => p.Start))
        {
            if (change.Start < position) throw new InvalidDataException("Overlapping edits.");
            stream.Write(payload, position, change.Start - position);
            stream.Write(change.Data);
            position = change.Start + change.Length;
        }
        stream.Write(payload, position, payload.Length - position);
        byte[] data = stream.ToArray();
        byte[] result = Pack(w => { w.Write(data.Length); w.Write(data); w.Write(64); w.Write(SHA512.HashData(data)); });
        _ = new SaveFile(result);
        return result;
    }

    public static byte[] Pack(Action<BinaryWriter> write)
    {
        using var s = new MemoryStream();
        using (var w = new BinaryWriter(s, Encoding.UTF8, true)) write(w);
        return s.ToArray();
    }
    private static int Pos(BinaryReader r) => checked((int)r.BaseStream.Position);
    private static void Skip(BinaryReader r, int length)
    {
        if (length < 0 || length > r.BaseStream.Length - r.BaseStream.Position) throw new EndOfStreamException();
        r.BaseStream.Position += length;
    }
    private static int Count(BinaryReader r, int max = 1000000)
    {
        int n = r.ReadInt32();
        if (n < 0 || n > max) throw new InvalidDataException($"Invalid count {n} at {Pos(r) - 4}.");
        return n;
    }
    private static byte[] ReadBlob(BinaryReader r)
    {
        int length = Count(r, 256 * 1024 * 1024);
        if (length > r.BaseStream.Length - r.BaseStream.Position) throw new EndOfStreamException();
        return r.ReadBytes(length);
    }
    private static void Strings(BinaryReader r)
    {
        int count = Count(r);
        for (int i = 0; i < count; i++) r.ReadString();
    }
    private static List<string> ReadStrings(BinaryReader r)
    {
        int count = Count(r);
        var list = new List<string>(count);
        for (int i = 0; i < count; i++) list.Add(r.ReadString());
        return list;
    }
    private static void StringValues(BinaryReader r)
    {
        int count = Count(r);
        for (int i = 0; i < count; i++) { r.ReadString(); Skip(r, 4); }
    }
    private static int Integer(string s, int min, int max) => int.TryParse(s, out int n) && n >= min && n <= max ? n : throw new InvalidDataException($"Expected integer {min}..{max}.");
    private static decimal Decimal(string s, decimal min, decimal max) => decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal n) && n >= min && n <= max ? n : throw new InvalidDataException($"Expected number {min}..{max}.");
    private static float Number(string s, float min, float max) => float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && float.IsFinite(n) && n >= min && n <= max ? n : throw new InvalidDataException($"Expected number {min}..{max}.");
    private static string SkillName(int id) => id switch
    {
        1 => "Swords", 2 => "Knives", 3 => "Clubs", 4 => "Polearms", 5 => "Spears", 6 => "Blocking", 7 => "Axes", 8 => "Bows",
        9 => "Elemental magic", 10 => "Blood magic", 11 => "Unarmed", 12 => "Pickaxes", 13 => "Wood cutting", 14 => "Crossbows",
        100 => "Jump", 101 => "Sneak", 102 => "Run", 103 => "Swim", 104 => "Fishing", 105 => "Cooking", 106 => "Farming",
        107 => "Crafting", 108 => "Dodge", 110 => "Ride", _ => $"Skill {id}"
    };
}
