using System.Text.Json;

namespace ValheimEditor;

public enum QualityLevel { Catalog = 0, Sensible = 1, Lunatic = 2 }

public static class QualityMode
{
    public static QualityLevel Level { get; set; } = QualityLevel.Catalog;
    public const int SensibleMax = 99;
    public const int LunaticMax = 999;
    public const int FormatMax = 65535;

    public static int MaxQuality(ItemDefinition definition) => Level switch
    {
        QualityLevel.Catalog => definition.MaxQuality,
        QualityLevel.Sensible => Math.Min(SensibleMax, FormatMax),
        QualityLevel.Lunatic => Math.Min(LunaticMax, FormatMax),
        _ => definition.MaxQuality
    };
}

public sealed record ItemDefinition(string Prefab, string Name, int MaxStack, int MaxQuality, float Durability, float DurabilityPerLevel)
{
    public string? EnglishName { get; init; }
    public string Description { get; init; } = "";
    public string EnglishDescription { get; init; } = "";
    public float Weight { get; init; }
    public float ScaleWeightByQuality { get; init; }
    public Dictionary<string, float> Damages { get; init; } = [];
    public Dictionary<string, float> DamagesPerLevel { get; init; } = [];
    public float Armor { get; init; }
    public float ArmorPerLevel { get; init; }
    public float Block { get; init; }
    public float BlockPerLevel { get; init; }
    public float FoodHealth { get; init; }
    public float FoodStamina { get; init; }
    public float FoodEitr { get; init; }
    public float FoodDuration { get; init; }
    public float FoodRegen { get; init; }
    public float UseDurability { get; init; }
    public IEnumerable<(string Label, string Value)> Stats(int quality, int quantity)
    {
        quality = Math.Clamp(quality, 1, QualityMode.MaxQuality(this));
        string N(float value) => value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
        string Leveled(float basis, float step) => N(basis) + (quality > 1 && step != 0 ? $" ({(step > 0 ? "+" : "")}{N(step * (quality - 1))}) = {N(basis + step * (quality - 1))}" : "");
        float weight = Weight * (ScaleWeightByQuality != 0 ? quality : 1);
        yield return (Appearance.T("Вес / шт.", "Weight / item"), N(weight));
        yield return (Appearance.T("Вес стака", "Stack weight"), N(weight * quantity));
        foreach (var (key, value) in Damages)
        {
            float step = DamagesPerLevel.GetValueOrDefault(key);
            if (value == 0 && step == 0) continue;
            string label = key switch
            {
                "m_blunt" => Appearance.T("Дробящий", "Blunt"), "m_slash" => Appearance.T("Рубящий", "Slash"),
                "m_pierce" => Appearance.T("Колющий", "Pierce"), "m_chop" => Appearance.T("Рубка деревьев", "Chop"),
                "m_pickaxe" => Appearance.T("Добыча", "Pickaxe"), "m_fire" => Appearance.T("Огонь", "Fire"),
                "m_frost" => Appearance.T("Мороз", "Frost"), "m_lightning" => Appearance.T("Молния", "Lightning"),
                "m_poison" => Appearance.T("Яд", "Poison"), "m_spirit" => Appearance.T("Дух", "Spirit"),
                _ => key.Replace("m_", "")
            };
            yield return (label, Leveled(value, step));
        }
        if (Category == "Armor" && Armor > 0) yield return (Appearance.T("Броня", "Armor"), Leveled(Armor, ArmorPerLevel));
        if ((Category is "Armor" or "Weapons") && Block > 0) yield return (Appearance.T("Блокирование", "Block armor"), Leveled(Block, BlockPerLevel));
        if (UseDurability != 0) yield return (Appearance.T("Макс. прочность", "Max durability"), Leveled(Durability, DurabilityPerLevel));
        if (Category == "Food")
        {
            yield return (Appearance.T("Здоровье", "Health"), N(FoodHealth));
            yield return (Appearance.T("Выносливость", "Stamina"), N(FoodStamina));
            if (FoodEitr > 0) yield return (Appearance.T("Эйтр", "Eitr"), N(FoodEitr));
            yield return (Appearance.T("Длительность, мин", "Duration, min"), N(FoodDuration / 60));
            yield return (Appearance.T("Регенерация HP / тик", "HP regeneration / tick"), N(FoodRegen));
        }
    }
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
    public byte[] Snapshot()
    {
        byte[] encoded = Encode().ToArray();
        encoded[4] = (byte)X; encoded[5] = (byte)Y;
        encoded[^1] = (byte)((encoded[^1] & ~1) | (Cheated ? 1 : 0));
        return encoded;
    }
}
