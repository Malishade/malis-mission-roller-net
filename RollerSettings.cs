using AOSharp.Common.GameData;
using AOSharp.Core.UI;
using Newtonsoft.Json;
using System.Text.Json;

public class RollerSettings
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AOSharp", "MalisMissionRoller", "settings.json"
    );

    [JsonProperty("easyHard")] public float EasyHard { get; set; } = 6;
    [JsonProperty("goodBad")] public float GoodBad { get; set; } = 0;
    [JsonProperty("orderChaos")] public float OrderChaos { get; set; } = 0;
    [JsonProperty("openHidden")] public float OpenHidden { get; set; } = 0;
    [JsonProperty("physicalMystical")] public float PhysicalMystical { get; set; } = 0;
    [JsonProperty("headonStealth")] public float HeadonStealth { get; set; } = 0;
    [JsonProperty("creditsXp")] public float CreditsXp { get; set; } = 0;

    [JsonProperty("returnItem")] public bool ReturnItem { get; set; } = true;
    [JsonProperty("killTarget")] public bool KillTarget { get; set; } = true;
    [JsonProperty("useItem")] public bool UseItem { get; set; } = true;
    [JsonProperty("findItem")] public bool FindItem { get; set; } = true;

    [JsonProperty("autoAdjustLvlSlider")] public bool AutoAdjustLvlSlider { get; set; } = true;
    [JsonProperty("removeRolledEntries")] public bool RemoveRolledEntries { get; set; } = true;
    [JsonProperty("autoAcceptMissions")] public bool AutoAcceptMissions { get; set; } = true;
    [JsonProperty("showPlayfieldBounds")] public bool ShowPlayfieldBounds { get; set; } = true;

    [JsonIgnore]
    public List<int> ActivePlayfields { get; set; } = new();

    [JsonProperty("locations")]
    public Dictionary<int, LocationFilter> Locations { get; set; } = MISSION_PLAYFIELDS.ToDictionary(
        pfId => pfId,
        pfId => new LocationFilter { Active = true, X1 = 0, Y1 = 0, X2 = 9999, Y2 = 9999 }
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

    public void SetSlider(int index, float value)
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
    public void EnablePlayfield(int pfId, bool state)
    {
        if (!Locations.TryGetValue(pfId, out var loc)) return;
        Locations[pfId] = loc with { Active = state };
        Save();
    }

    public void RemoveBounds(int pfId)
    {
        if (!Locations.TryGetValue(pfId, out var loc)) return;
        Locations[pfId] = new LocationFilter { Active = loc.Active, X1 = 0, Y1 = 0, X2 = 0, Y2 = 0 };
        Save();
    }

    public void UpdateLocation(int pfId, int x1, int y1, int x2, int y2)
    {
        Chat.WriteLine(x1);
        var existing = Locations.TryGetValue(pfId, out var loc) ? loc : new LocationFilter();
        Locations[pfId] = new LocationFilter
        {
            Active = existing.Active,
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
        };
        Save();
    }

    public void UpdateLocation(int pfId, Vector3 startPos, Vector3 endPos)
    {
        UpdateLocation(pfId, (int)startPos.X, (int)startPos.Z, (int)endPos.X, (int)endPos.Z);
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
    [JsonProperty("active")] public bool Active { get; init; }
    [JsonProperty("x1")] public int X1 { get; init; }
    [JsonProperty("y1")] public int Y1 { get; init; }
    [JsonProperty("x2")] public int X2 { get; init; }
    [JsonProperty("y2")] public int Y2 { get; init; }

    public bool Contains(int x, int y) =>
        x >= Math.Min(X1, X2) && x <= Math.Max(X1, X2) &&
        y >= Math.Min(Y1, Y2) && y <= Math.Max(Y1, Y2);
}