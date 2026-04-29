using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using AOSharp.Core.UI;
using SmokeLounge.AOtomation.Messaging.GameData;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ItemEntry
{
    [JsonPropertyName("low_id")] public int LowId { get; set; }
    [JsonPropertyName("high_id")] public int HighId { get; set; }
    [JsonPropertyName("ql")] public int Ql { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }

    [JsonIgnore] private string? _name;
    [JsonIgnore] public string? Name => _name ??= Item.TryGet(LowId, HighId, Ql, out ACGItem item) ? item.Name : null;

}

public static class RollListProcessor
{
    private const int SpecialCreditItemId = 297315;
    private const int NanoCrystalTolerance = 10;
    private const int MaxQl = 200;
    private static List<List<int>> _missionLvlsRaw;
    private static List<int> _missionLevels => _missionLvlsRaw[DynelManager.LocalPlayer.Level - 1];

    public static void Init(string pluginDir)
    {
        try
        {
            var path = $"{pluginDir}\\MissionLevels.json";
            var json = File.ReadAllText(path);
            _missionLvlsRaw = JsonSerializer.Deserialize<List<List<int>>>(json);

        }
        catch (Exception ex)
        {
            Chat.WriteLine($"Deserialize failed: {ex.GetType().Name}");
            Chat.WriteLine(ex.Message);
        }

    }

    public static IEnumerable<Identity> Check(IEnumerable<MissionInfo> missions, IEnumerable<ItemEntry> items)
    {
        return missions
            .Where(mission => items.Any(item => MissionContainsItem(mission, item)))
            .Select(mission => mission.MissionIdentity);
    }

    private static bool MissionContainsItem(MissionInfo mission, ItemEntry item)
    {
        return mission.MissionItemData.Any(e => e.HighId == item.HighId && e.Ql == item.Ql) ||
              item.Name != null && mission.Description.Contains(item.Name);
    }
    public static bool TryGetDifficultySliderValue(IEnumerable<ItemEntry> items, out byte value)
    {
        value = 0;

        var item = FindNextRollable(items);

        if (item == null)
            return false;

        if (item.LowId == SpecialCreditItemId)
        {
            value = (byte)_missionLevels.IndexOf(item.Ql);
            return true;
        }

        var missionLevel = DetermineMissionLevel(item);
        value = (byte)(_missionLevels.IndexOf(missionLevel) + 1);
        return true;
    }

    private static ItemEntry FindNextRollable(IEnumerable<ItemEntry> items)
    {
        return items.FirstOrDefault(item => IsRollable(item));
    }

    private static bool IsRollable(ItemEntry item)
    {
        if (item.LowId == SpecialCreditItemId)
            return true;

        if (DynelManager.LocalPlayer.Level > MaxQl && item.Ql == MaxQl && !IsNanoCrystal(item.Name))
            return true;

        return _missionLevels.Any(lvl => IsQlMatch(item.Ql, lvl, IsNanoCrystal(item.Name)));
    }

    private static int DetermineMissionLevel(ItemEntry item)
    {
        if (DynelManager.LocalPlayer.Level > MaxQl && item.Ql == MaxQl && !IsNanoCrystal(item.Name))
            return _missionLevels.First(lvl => lvl >= MaxQl);

        return _missionLevels.OrderBy(lvl => Math.Abs(lvl - item.Ql)).First();
    }

    private static bool IsQlMatch(int itemQl, int missionLevel, bool isNanoCrystal)
    {
        return isNanoCrystal ? IsWithinNanoCrystalRange(itemQl, missionLevel) : itemQl == missionLevel;
    }
    private static bool IsWithinNanoCrystalRange(int itemQl, int missionLevel)
    {
        return Math.Abs(itemQl - missionLevel) <= NanoCrystalTolerance;
    }
    private static bool IsNanoCrystal(string? name)
    {
        if (name == null)
            return false;

        return name.Contains("Nano Crystal") || name.Contains("NanoCrystal");
    }
}