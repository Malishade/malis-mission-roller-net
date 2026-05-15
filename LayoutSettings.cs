using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class LayoutSettings
{
    [JsonProperty("dbFilters")]
    public DbFilters DbFilters { get; set; } = new();

    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AOSharp", "MalisMissionRoller", "layout.json"
    );

    [JsonProperty("windowPos")] public WindowPos WindowPos { get; set; } = new() { X = 80, Y = 40 };
    [JsonProperty("windowWidth")] public int WindowWidth { get; set; } = 720;
    [JsonProperty("windowHeight")] public int WindowHeight { get; set; } = 600;
    [JsonProperty("activeTabId")] public string ActiveTabId { get; set; } = "tab-general";
    [JsonProperty("detached")] public JObject Detached { get; set; } = new();
    [JsonProperty("tabs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<TabEntry> Tabs { get; set; } = new()
    {
        new() { Id = "tab-general",      Label = "General",      Sections = ["missions", "rollList", "settings"] },
        new() { Id = "tab-item-browser", Label = "Item Browser", Sections = ["db"] },
    };

    public void Save(string rawJson)
    {
        var incoming = JsonConvert.DeserializeObject<LayoutSettings>(rawJson);
        if (incoming == null) return;

        WindowPos = incoming.WindowPos;
        WindowWidth = incoming.WindowWidth;
        WindowHeight = incoming.WindowHeight;
        ActiveTabId = incoming.ActiveTabId;
        Detached = incoming.Detached;
        Tabs = incoming.Tabs;
        DbFilters = incoming.DbFilters ?? new();

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllText(Path, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    public static LayoutSettings Load()
    {
        try
        {
            return JsonConvert.DeserializeObject<LayoutSettings>(
                File.ReadAllText(Path)
            )!;
        }
        catch
        {
            var s = new LayoutSettings();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            File.WriteAllText(Path, JsonConvert.SerializeObject(s, Formatting.Indented));
            return s;
        }
    }

    public string ToJson() => JsonConvert.SerializeObject(new
    {
        type = "layoutState",
        windowPos = WindowPos,
        windowWidth = WindowWidth,
        windowHeight = WindowHeight,
        activeTabId = ActiveTabId,
        detached = Detached,
        tabs = Tabs,
        dbFilters = DbFilters,
    });
}

public class WindowPos
{
    [JsonProperty("x")] public int X { get; set; }
    [JsonProperty("y")] public int Y { get; set; }
}

public class TabEntry
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("label")] public string Label { get; set; }
    [JsonProperty("sections", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> Sections { get; set; } = new();
}
public class DbFilters
{
    [JsonProperty("canFilter", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> CanFilter { get; set; } = new();

    [JsonProperty("itemFilter", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> ItemFilter { get; set; } = new();

    [JsonProperty("modFilter", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<ModFilterEntry> ModFilter { get; set; } = new();

    [JsonProperty("statFilter", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<StatFilterEntry> StatFilter { get; set; } = new();

    [JsonProperty("reqFilter", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<StatFilterEntry> ReqFilter { get; set; } = new();
}

public class StatFilterEntry
{
    [JsonProperty("statId")] public int StatId { get; set; }
    [JsonProperty("min")] public int? Min { get; set; }
    [JsonProperty("max")] public int? Max { get; set; }
    [JsonProperty("enabled")] public bool Enabled { get; set; } = true;
}

public class ModFilterEntry
{
    [JsonProperty("uid")] public string Uid { get; set; }
    [JsonProperty("modType")] public string ModType { get; set; }
    [JsonProperty("enabled")] public bool Enabled { get; set; } = true;

    // Only present for "stat" and "lock" types
    [JsonProperty("statId")] public int? StatId { get; set; }

    [JsonProperty("min")] public int? Min { get; set; }
    [JsonProperty("max")] public int? Max { get; set; }
}