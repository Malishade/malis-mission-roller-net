using Newtonsoft.Json;

public class RollList
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AOSharp", "MalisMissionRoller", "rolllist.json"
    );

    public List<ItemEntry> Items { get; private set; } = new();

    // ── Granular mutators ─────────────────────────────────────────────────────

    public void Add(ItemEntry item)
    {
        var existing = Items.FirstOrDefault(i => i.LowId == item.LowId && i.HighId == item.HighId);
        if (existing != null)
            existing.Count++;
        else
        {
            item.Count = 1;
            Items.Add(item);
        }
        Save();
    }

    public void Remove(int lowId, int highId)
    {
        Items.RemoveAll(i => i.LowId == lowId && i.HighId == highId);
        Save();
    }

    public void AdjustCount(int lowId, int highId, int delta)
    {
        var item = Items.FirstOrDefault(i => i.LowId == lowId && i.HighId == highId);
        if (item == null) return;
        item.Count = Math.Max(1, item.Count + delta);
        Save();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllText(Path, JsonConvert.SerializeObject(Items, Formatting.Indented));
    }

    public static RollList Load()
    {
        try { return new RollList { Items = JsonConvert.DeserializeObject<List<ItemEntry>>(File.ReadAllText(Path)) }; }
        catch { return new RollList(); }
    }
}