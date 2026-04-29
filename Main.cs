using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.UI;
using SmokeLounge.AOtomation.Messaging.GameData;
using System.Text.Json;

public class Main : AOPluginEntry
{
    private static MissionRoller _roller;
    private static BoundsProcessor _boundsProcessor;
    private static WebSocketServer _server;
    private static RollList _rollList;
    private static RollerSettings _settings;
    private static AoDbServer _aoDbServer;
    private static int? _pendingBoundsPfId;

    public override void Run()
    {
        Chat.WriteLine("Mali's Mission Roller - Net");
        Chat.WriteLine("hnmnge");
        _aoDbServer = new AoDbServer($"{PluginDirectory}\\items.db");
        _aoDbServer.Start();

        //RollListProcessor.Init(PluginDirectory);

        ////_settings = RollerSettings.Load();
        //_boundsProcessor = new BoundsProcessor();
        //_roller = new MissionRoller();
        //_rollList = RollList.Load();
        //_server = new WebSocketServer(7069);

        //HookServerEvents();
        //Mission.RollListChanged += OnRollListChanged;

        //_server.Start();
        //AODBItemServer.StartServer();
    }

    private static void HookServerEvents()
    {
        _server.OnClientConnected += () =>
        {
            BroadcastState();
            BroadcastRollList();
        };

        _server.OnToggleMissionType += label =>
        {
            _settings.ToggleMissionType(label);
            BroadcastState();
        };

        _server.OnToggleExtra += label =>
        {
            _settings.ToggleExtra(label);
            BroadcastState();
        };

        _server.OnSetSlider += (index, value) =>
        {
            _settings.SetSlider(index, value);
            BroadcastState();
        };

        _server.OnTogglePlayfield += pfId =>
        {
            _settings.TogglePlayfield(pfId);
            BroadcastState();
        };

        _server.OnRemoveBounds += pfId =>
        {
            _settings.RemoveBounds(pfId);
            BroadcastState();
        };

        _server.OnAddRollItem += item =>
        {
            _rollList.Add(item);
            _roller.UpdateItems(_rollList.Items);
            BroadcastRollList();
        };

        _server.OnRemoveRollItem += (lowId, highId) =>
        {
            _rollList.Remove(lowId, highId);
            _roller.UpdateItems(_rollList.Items);
            BroadcastRollList();
        };

        _server.OnAdjustRollCount += (lowId, highId, delta) =>
        {
            _rollList.AdjustCount(lowId, highId, delta);
            _roller.UpdateItems(_rollList.Items);
            BroadcastRollList();
        };

        _server.OnStartBounds += pfId =>
        {
            _pendingBoundsPfId = pfId;
            _boundsProcessor.Start();
        };

        _server.OnConfirmBounds += pfId =>
        {
            if (_pendingBoundsPfId != pfId)
                return;

            _pendingBoundsPfId = null;

            _boundsProcessor.Stop(out Vector3 startPos, out Vector3 endPos);
            _settings.UpdateLocation(pfId, startPos, endPos);

            BroadcastState();
        };

        _server.OnStart += () => _roller.Start();
        _server.OnStop += () => _roller.Stop();
    }

    private static void BroadcastState()
    {
        _server.Broadcast(JsonSerializer.Serialize(new
        {
            type = "state",
            settings = _settings
        }));
    }

    private static void BroadcastRollList()
    {
        _server.Broadcast(JsonSerializer.Serialize(new
        {
            type = "rollListState",
            items = _rollList.Items
        }));
    }

    private static void OnRollListChanged(object sender, RollListChangedArgs e)
    {
        _roller.ResetTick();
        _server.Broadcast(SerializeMissions([.. e.MissionDetails]));
        _roller.CheckHits(e.MissionDetails);
    }

    private static string SerializeMissions(List<MissionInfo> missions)
    {
        var list = missions.Select(m => new
        {
            id = m.MissionIdentity.Instance,
            name = m.Title,
            playfield = m.Playfield.Instance.ToString(),
            credits = m.Credits,
            creditsMax = m.Credits,
            xp = m.XpReward,
            rewards = m.MissionItemData?.Select(r => r.LowId).ToArray() ?? []
        });

        return JsonSerializer.Serialize(new
        {
            type = "missionResults",
            missions = list
        });
    }

    public override void Teardown()
    {
        Chat.WriteLine("Shutting down...");
        _aoDbServer.Stop();
        //_roller?.Stop();
        //_server?.Stop();
        //AODBItemServer.StopServer();
        //Mission.RollListChanged -= OnRollListChanged;
    }
}

