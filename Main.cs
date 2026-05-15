using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.UI;
using Newtonsoft.Json;
using SmokeLounge.AOtomation.Messaging.GameData;

public class Main : AOPluginEntry
{
    private static MissionRoller _roller;
    private static BoundsProcessor _boundsProcessor;
    private static WebSocketServer _server;
    private static AoDbServer _dbServer;
    private static RollList _rollList;
    private static RollerSettings _settings;
    private static LayoutSettings _layout;
    private static int? _pendingBoundsPfId;

    public override void Run()
    {
        Chat.WriteLine("Mali's Mission oller - Net");

        RollListProcessor.Init(PluginDirectory);
        _dbServer = new AoDbServer($"{PluginDirectory}\\items.db");
        _dbServer.Start();

        _settings = RollerSettings.Load();
        _rollList = RollList.Load();
        _layout = LayoutSettings.Load();

        _roller = new MissionRoller(_settings);
        _roller.UpdateItems(_rollList.Items);
        _boundsProcessor = new BoundsProcessor();
        _server = new WebSocketServer();

        HookServerEvents();
        Mission.RollListChanged += OnRollListChanged;

        _server.Start();
    }

    private static void HookServerEvents()
    {
        _server.OnClientConnected += () =>
        {
            _server.Broadcast(_layout.ToJson());
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

        _server.OnRemoveRollItem += item =>
        {
            _rollList.Remove(item);
            _roller.UpdateItems(_rollList.Items);
            BroadcastRollList();
        };

        _server.OnSetRollCount += (item, count) =>
        {
            _rollList.SetCount(item, count);
            _roller.UpdateItems(_rollList.Items);
            BroadcastRollList();
        };
        
        _server.OnStartBounds += () =>
        {
            _pendingBoundsPfId = Playfield.ModelIdentity.Instance;
            _boundsProcessor.Start();
        };

        _server.OnConfirmBounds += () =>
        {
            if (_pendingBoundsPfId == null)
                return;

            _boundsProcessor.Stop(out Vector3 startPos, out Vector3 endPos);
            _settings.UpdateLocation(_pendingBoundsPfId.Value, startPos, endPos);

            _pendingBoundsPfId = null;
            BroadcastState();
        };

        _server.OnSetBounds += (pfId, x1, y1, x2, y2) =>
        {
            _settings.UpdateLocation(pfId, x1, y1, x2, y2);
            BroadcastState();
        };


        _server.OnEnableAllPlayfields += () =>
        {
            foreach (var id in _settings.Locations.Keys)
                _settings.EnablePlayfield(id, true);

            BroadcastState();
        };

        _server.OnDisableAllPlayfields += () =>
        {
            foreach (var id in _settings.Locations.Keys)
                _settings.EnablePlayfield(id, false);

            BroadcastState();
        };

        _server.OnSaveLayout += (json) =>
        {
            _layout.Save(json);
        };

        _server.OnStart += () => _roller.Start();
        _server.OnStop += () => _roller.Stop();
    }

    private static void BroadcastState()
    {
        _server.Broadcast(JsonConvert.SerializeObject(new
        {
            type = "state",
            settings = _settings
        }));
    }

    private static void BroadcastRollList()
    {
        _server.Broadcast(JsonConvert.SerializeObject(new
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
            icon = m.MissionIcon,
            id = m.MissionIdentity.Instance,
            name = m.Title,
            playfield = m.Playfield.Instance.ToString(),
            credits = m.Credits,
            creditsMax = m.Credits,
            xp = m.XpReward,
            description = m.Description,
            rewards = m.MissionItemData?.Select(r => new
            {
                ids = new[] { r.LowId, r.HighId },
                ql = r.Ql
            }).ToArray() ?? Array.Empty<object>()
        });

        return JsonConvert.SerializeObject(new
        {
            type = "missionResults",
            missions = list
        });
    }

    public override void Teardown()
    {
        Chat.WriteLine("Shutting down...");
        _roller?.Stop();
        _server?.Stop();
        _dbServer?.Stop();
        Mission.RollListChanged -= OnRollListChanged;
    }
}

