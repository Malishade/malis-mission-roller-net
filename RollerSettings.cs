using AOSharp.Common.GameData;
using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;

public class RollerSettings
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AOSharp", "MalisMissionRoller", "settings.json"
    );

    [JsonPropertyName("easyHard")] public int EasyHard { get; set; } = 6;
    [JsonPropertyName("goodBad")] public int GoodBad { get; set; } = 0;
    [JsonPropertyName("orderChaos")] public int OrderChaos { get; set; } = 0;
    [JsonPropertyName("openHidden")] public int OpenHidden { get; set; } = 0;
    [JsonPropertyName("physicalMystical")] public int PhysicalMystical { get; set; } = 0;
    [JsonPropertyName("headonStealth")] public int HeadonStealth { get; set; } = 0;
    [JsonPropertyName("creditsXp")] public int CreditsXp { get; set; } = 0;

    [JsonPropertyName("returnItem")] public bool ReturnItem { get; set; } = true;
    [JsonPropertyName("killTarget")] public bool KillTarget { get; set; } = true;
    [JsonPropertyName("useItem")] public bool UseItem { get; set; } = true;
    [JsonPropertyName("findItem")] public bool FindItem { get; set; } = true;

    [JsonPropertyName("autoAdjustLvlSlider")] public bool AutoAdjustLvlSlider { get; set; } = true;
    [JsonPropertyName("removeRolledEntries")] public bool RemoveRolledEntries { get; set; } = true;
    [JsonPropertyName("autoAcceptMissions")] public bool AutoAcceptMissions { get; set; } = true;
    [JsonPropertyName("showPlayfieldBounds")] public bool ShowPlayfieldBounds { get; set; } = true;

    [JsonIgnore]
    public List<int> ActivePlayfields { get; set; } = new();

    [JsonPropertyName("locations")]
    public Dictionary<int, LocationFilter> Locations { get; set; } = MISSION_PLAYFIELDS.ToDictionary(
        pfId => pfId,
        pfId => new LocationFilter { Active = true, X1 = 0, X2 = 0, Y1 = 0, Y2 = 0 }
    );

    private static readonly int[] MISSION_PLAYFIELDS = [760, 585, 655, 550, 545, 505, 605, 800, 665, 590, 670, 595, 620, 685, 687, 717, 647, 791, 695, 625, 560, 696, 567, 566, 565, 540, 716, 705, 700, 710, 570, 630, 735, 740, 730, 610, 615, 635, 790, 795, 640, 646, 650, 600, 551, 586];

    public void ToggleMissionType(string label)
    {
        switch (label)
        {
            case "Return Item": ReturnItem = !ReturnItem; break;
            case "Kill Target": KillTarget = !KillTarget; break;
            case "Use Item": UseItem = !UseItem; break;
            case "Find Item": FindItem = !FindItem; break;
        }
        Save();
    }

    public void ToggleExtra(string label)
    {
        switch (label)
        {
            case "Auto Adjust Lvl Slider": AutoAdjustLvlSlider = !AutoAdjustLvlSlider; break;
            case "Remove Rolled Entries": RemoveRolledEntries = !RemoveRolledEntries; break;
            case "Auto Accept Missions": AutoAcceptMissions = !AutoAcceptMissions; break;
            case "Show Playfield Bounds": ShowPlayfieldBounds = !ShowPlayfieldBounds; break;
        }
        Save();
    }

    public void SetSlider(int index, int value)
    {
        switch (index)
        {
            case 0: EasyHard = value; break;
            case 1: GoodBad = value; break;
            case 2: OrderChaos = value; break;
            case 3: OpenHidden = value; break;
            case 4: PhysicalMystical = value; break;
            case 5: HeadonStealth = value; break;
            case 6: CreditsXp = value; break;
        }
        Save();
    }

    public void TogglePlayfield(int pfId)
    {
        if (!Locations.TryGetValue(pfId, out var loc)) return;
        Locations[pfId] = loc with { Active = !loc.Active };
        Save();
    }

    public void RemoveBounds(int pfId)
    {
        if (!Locations.TryGetValue(pfId, out var loc)) return;
        Locations[pfId] = new LocationFilter { Active = loc.Active, X1 = 0, Y1 = 0, X2 = 0, Y2 = 0 };
        Save();
    }

    public void UpdateLocation(int pfId, Vector3 startPos, Vector3 endPos)
    {
        var existing = Locations.TryGetValue(pfId, out var loc) ? loc : new LocationFilter();
        Locations[pfId] = new LocationFilter
        {
            Active = existing.Active,
            X1 = startPos.X,
            Y1 = startPos.Y,
            X2 = endPos.X,
            Y2 = endPos.Y,
        };
        Save();
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllText(Path, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    public static RollerSettings Load()
    {
        try
        {
            return JsonConvert.DeserializeObject<RollerSettings>(
                File.ReadAllText(Path)
            )!;
        }
        catch
        {
            var s = new RollerSettings();
            s.Save();
            return s;
        }
    }
}

public record LocationFilter
{
    [JsonPropertyName("active")] public bool Active { get; init; }
    [JsonPropertyName("x1")] public float X1 { get; init; }
    [JsonPropertyName("y1")] public float Y1 { get; init; }
    [JsonPropertyName("x2")] public float X2 { get; init; }
    [JsonPropertyName("y2")] public float Y2 { get; init; }

    public bool Contains(float x, float y) =>
        x >= Math.Min(X1, X2) && x <= Math.Max(X1, X2) &&
        y >= Math.Min(Y1, Y2) && y <= Math.Max(Y1, Y2);
}