using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.Misc;
using AOSharp.Core.UI;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

public class MissionRoller
{
    private readonly AutoResetInterval _rollTick = new(1500);
    private List<ItemEntry> _items = new();

    public void Start() => Game.OnUpdate += OnUpdate;
    public void Stop() => Game.OnUpdate -= OnUpdate;

    public void UpdateItems(List<ItemEntry> items) => _items = items;

    public void ResetTick() => _rollTick.Reset();

    public void CheckHits(IEnumerable<MissionInfo> missions)
    {
        var hits = RollListProcessor.Check(missions, _items);
        if (!hits.Any()) return;

        Network.Send(new CreateQuestMessage { MissionId = hits.First() });
    }

    private void OnUpdate(object sender, float e)
    {
        if (!_rollTick.Elapsed) return;

        if (RollListProcessor.TryGetDifficultySliderValue(_items, out var difficultyValue))
            RollerTerminal.Use(difficultyValue);
    }
}

public class BoundsProcessor
{
    private Vector3 _startPos = Vector3.Zero;
    private Vector3 _endPos = Vector3.Zero;

    public void Start()
    {
        _endPos = Vector3.Zero;
        _startPos = DynelManager.LocalPlayer.Position;
        Game.OnUpdate += OnUpdate;
    }

    public void Stop(out Vector3 startPos, out Vector3 endPos)
    {
        _endPos = DynelManager.LocalPlayer.Position;
        startPos = _startPos;
        endPos = _endPos;
        Game.OnUpdate -= OnUpdate;
    }

    private void OnUpdate(object sender, float deltaTime)
    {
        DrawBounds(_startPos, DynelManager.LocalPlayer.Position, new Vector3(0, 1, 0));
    }

    public void DrawBounds(Vector3 start, Vector3 end, Vector3 color)
    {
        float y = Math.Min(start.Y, end.Y);

        Vector3 a = new(start.X, y, start.Z);
        Vector3 b = new(end.X, y, start.Z);
        Vector3 c = new(end.X, y, end.Z);
        Vector3 d = new(start.X, y, end.Z);

        Debug.DrawLine(a, b, color);
        Debug.DrawLine(b, c, color);
        Debug.DrawLine(c, d, color);
        Debug.DrawLine(d, a, color);
    }
}