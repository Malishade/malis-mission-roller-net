using Newtonsoft.Json;

public class RollList
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AOSharp", "MalisMissionRoller", "rolllist.json"
    );

    public List<RollerItemEntry> Items { get; private set; } = new();


    public void Add(ItemEntry entry)
    {
        var existing = Items.FirstOrDefault(i => i.Item == entry);

        if (existing != null)
        {
            existing.Count++;
        }
        else
        {
            Items.Add(new RollerItemEntry
            {
                Count = 1,
                Item = entry
            });
        }

        Save();
    }

    public void Remove(ItemEntry entry)
    {
        Items.RemoveAll(i => i.Item == entry);
        Save();
    }

    public void SetCount(ItemEntry entry, int count)
    {
        var item = Items.FirstOrDefault(i => i.Item == entry);
        if (item == null) return;
        item.Count = count;
        Save();
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllText(Path, JsonConvert.SerializeObject(Items, Formatting.Indented));
    }

    public static RollList Load()
    {
        try { return new RollList { Items = JsonConvert.DeserializeObject<List<RollerItemEntry>>(File.ReadAllText(Path)) }; }
        catch { return new RollList(); }
    }
}