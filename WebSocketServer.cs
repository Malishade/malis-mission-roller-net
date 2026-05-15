using AOSharp.Core.UI;
using Newtonsoft.Json;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly List<WebSocket> _clients = new();
    private readonly CancellationTokenSource _cts = new();
    private Task _listenerTask;

    public event Action OnClientConnected;
    public event Action OnStart;
    public event Action OnStop;

    public event Action<string> OnToggleMissionType;
    public event Action<string> OnToggleExtra;
    public event Action<int, float> OnSetSlider;
    public event Action<int> OnTogglePlayfield;
    public event Action<int> OnRemoveBounds;
    public event Action OnStartBounds;
    public event Action OnConfirmBounds;
    public event Action OnEnableAllPlayfields;
    public event Action OnDisableAllPlayfields;

    public event Action<ItemE> OnAddRollItem;
    public event Action<ItemE> OnRemoveRollItem;
    public event Action<ItemE, int> OnSetRollCount;
    public event Action<int, int, int, int, int> OnSetBounds;

    public event Action<string> OnSaveLayout;

    public WebSocketServer(int port = 7069)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            Chat.WriteLine("WS server started");
        }
        catch (Exception ex)
        {
            Chat.WriteLine($"WS start failed: {ex.Message}");
            return;
        }

        _listenerTask = Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested && _listener.IsListening)
                {
                    var ctx = await _listener.GetContextAsync();
                    if (ctx.Request.IsWebSocketRequest)
                    {
                        var wsCtx = await ctx.AcceptWebSocketAsync(null);
                        _clients.Add(wsCtx.WebSocket);
                        OnClientConnected?.Invoke();
                        _ = HandleClient(wsCtx.WebSocket);
                    }
                }
            }
            catch (HttpListenerException) { }
        });
    }

    public void Broadcast(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        foreach (var client in _clients.ToList())
        {
            if (client.State == WebSocketState.Open)
                client.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
        }
    }

    public void Stop()
    {
        OnClientConnected = null;
        OnStart = null;
        OnStop = null;
        OnToggleMissionType = null;
        OnToggleExtra = null;
        OnSetSlider = null;
        OnTogglePlayfield = null;
        OnRemoveBounds = null;
        OnStartBounds = null;
        OnConfirmBounds = null;
        OnAddRollItem = null;
        OnRemoveRollItem = null;
        OnSetRollCount = null;
        OnSaveLayout = null;

        _cts.Cancel();
        try { _listener.Stop(); } catch { }

        foreach (var c in _clients)
        {
            try
            {
                if (c.State == WebSocketState.Open)
                    c.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", CancellationToken.None).Wait(500);
            }
            catch { }
            finally { c.Dispose(); }
        }

        _clients.Clear();
        try { _listenerTask?.Wait(1000); } catch { }
    }

    private async Task HandleClient(WebSocket ws)
    {
        var buffer = new byte[4096];

        while (ws.State == WebSocketState.Open)
        {
            try
            {
                var result = await ws.ReceiveAsync(buffer, _cts.Token);
                if (result.Count == 0 || result.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;

                Chat.WriteLine(json);

                switch (root.GetProperty("type").GetString())
                {
                    case "toggleMissionType":
                        OnToggleMissionType?.Invoke(root.GetProperty("value").GetString());
                        break;

                    case "toggleExtra":
                        OnToggleExtra?.Invoke(root.GetProperty("value").GetString());
                        break;

                    case "setSlider":
                        OnSetSlider?.Invoke(
                            root.GetProperty("index").GetInt32(),
                            root.GetProperty("value").GetSingle()
                        );
                        break;

                    case "togglePlayfield":
                        OnTogglePlayfield?.Invoke(root.GetProperty("playfieldId").GetInt32());
                        break;

                    case "removeBounds":
                        OnRemoveBounds?.Invoke(root.GetProperty("playfieldId").GetInt32());
                        break;

                    case "enableAllPlayfields":
                        OnEnableAllPlayfields?.Invoke();
                        break;

                    case "disableAllPlayfields":
                        OnDisableAllPlayfields?.Invoke();
                        break;

                    case "startBounds":
                        OnStartBounds?.Invoke();
                        break;

                    case "confirmBounds":
                        OnConfirmBounds?.Invoke();
                        break;

                    case "addRollItem":
                        //var item = JsonConvert.DeserializeObject<ItemE>(
                        //    root.GetProperty("item").GetRawText()
                        //);
                        OnAddRollItem?.Invoke(JsonConvert.DeserializeObject<ItemE>(root.GetProperty("item").GetRawText()));
                        break;

                    case "removeRollItem":
                        //OnRemoveRollItem?.Invoke(
                        //    root.GetProperty("lowId").GetInt32(),
                        //    root.GetProperty("highId").GetInt32()
                        //);
                        OnRemoveRollItem.Invoke(JsonConvert.DeserializeObject<ItemE>(root.GetProperty("item").GetRawText()));
                        break;

                    case "setRollCount":
                        //OnSetRollCount?.Invoke(
                        //    root.GetProperty("lowId").GetInt32(),
                        //    root.GetProperty("highId").GetInt32(),
                        //    root.GetProperty("count").GetInt32()
                        //);

                        OnSetRollCount?.Invoke(JsonConvert.DeserializeObject<ItemE>(root.GetProperty("item").GetRawText()), root.GetProperty("count").GetInt32());
                        break;
                    case "setBounds":
                        OnSetBounds?.Invoke(
                            root.GetProperty("playfieldId").GetInt32(),
                            root.GetProperty("x1").GetInt32(),
                            root.GetProperty("y1").GetInt32(),
                            root.GetProperty("x2").GetInt32(),
                            root.GetProperty("y2").GetInt32()
                        );
                        break;
                    case "start":
                        OnStart?.Invoke();
                        break;

                    case "stop":
                        OnStop?.Invoke();
                        break;

                    case "saveLayout":
                        OnSaveLayout?.Invoke(json);
                        break;
                }
            }
            catch (Exception ex)
            {
                Chat.WriteLine($"HandleClient error: {ex.Message}");
            }
        }

        _clients.Remove(ws);
    }
}