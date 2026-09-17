using System.Text.Json;

namespace ValheimEditor;

public sealed record ItemDefinition(string Prefab, string Name, int MaxStack, int MaxQuality, float Durability, float DurabilityPerLevel)
{
    public string? EnglishName { get; init; }
    public string Category { get; init; } = "Other";
    public bool Internal { get; init; }
    public string Icon { get; init; } = "";
    public string DisplayName => Appearance.English ? EnglishName ?? Prefab : Name;
    public int Hash => StableHash(Prefab);
    public override string ToString() => DisplayName == Prefab ? Prefab : $"{DisplayName}  /  {Prefab}";
    public static int StableHash(string value)
    {
        unchecked
        {
            int a = 5381, b = a;
            for (int i = 0; i < value.Length; i += 2)
            {
                a = ((a << 5) + a) ^ value[i];
                if (i + 1 < value.Length) b = ((b << 5) + b) ^ value[i + 1];
            }
            return a + b * 1566083941;
        }
    }
}

public static class Catalog
{
    private static readonly Dictionary<int, Image?> images = [];
    public static Image? ImageFor(ItemDefinition? item)
    {
        if (item == null) return null;
        if (images.TryGetValue(item.Hash, out var image)) return image;
        using var stream = typeof(Catalog).Assembly.GetManifestResourceStream("Catalog.Icons." + item.Icon);
        if (stream != null)
        {
            using var decoded = Image.FromStream(stream);
            image = new Bitmap(decoded);
        }
        images[item.Hash] = image;
        return image;
    }
    public static IEnumerable<ItemDefinition> Filter(string query, string category, bool internalItems) => Items.Where(i =>
        (internalItems || !i.Internal) && (category == "All" || i.Category == category) &&
        (i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || (i.EnglishName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) || i.Prefab.Contains(query, StringComparison.OrdinalIgnoreCase))).OrderBy(i => i.DisplayName);
    public static readonly List<ItemDefinition> Items = LoadItems();
    private static List<ItemDefinition> LoadItems()
    {
        using var stream = typeof(Catalog).Assembly.GetManifestResourceStream("Catalog.items.json")
            ?? throw new InvalidDataException("Embedded item catalog is missing.");
        return (JsonSerializer.Deserialize<List<ItemDefinition>>(stream) ?? [])
            .Where(i => i.MaxStack is > 0 and <= 65535 && i.MaxQuality is > 0 and <= 65535).DistinctBy(i => i.Hash).OrderBy(i => i.Name).ToList();
    }
    public static ItemDefinition? Find(int hash) => Items.Find(i => i.Hash == hash);
}

public sealed class InventoryItem
{
    public bool Cheated { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Hash { get; init; }
    public Func<byte[]> Encode { get; init; } = null!;
    public Field? Quantity { get; init; }
    public Field? Quality { get; init; }
}
