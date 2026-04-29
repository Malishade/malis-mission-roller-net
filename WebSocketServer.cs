using AOSharp.Core.UI;
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

    // ── Events ───────────────────────────────────────────────────────────────
    public event Action OnClientConnected;
    public event Action OnStart;
    public event Action OnStop;

    // Settings
    public event Action<string> OnToggleMissionType;
    public event Action<string> OnToggleExtra;
    public event Action<int, int> OnSetSlider;          // (index, value)
    public event Action<int> OnTogglePlayfield;    // pfId
    public event Action<int> OnRemoveBounds;       // pfId
    public event Action<int> OnStartBounds;        // pfId
    public event Action<int> OnConfirmBounds;      // pfId

    // Roll list
    public event Action<ItemEntry> OnAddRollItem;
    public event Action<int, int> OnRemoveRollItem;     // (lowId, highId)
    public event Action<int, int, int> OnAdjustRollCount;    // (lowId, highId, delta)

    public WebSocketServer(int port)
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
        // 1. break event references
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
        OnAdjustRollCount = null;

        // 2. stop listener
        _cts.Cancel();

        try { _listener.Stop(); } catch { }

        // 3. close clients
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

        // 4. wait for background task
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
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                switch (root.GetProperty("type").GetString())
                {
                    // ── Settings ──────────────────────────────────────────────
                    case "toggleMissionType":
                        OnToggleMissionType?.Invoke(root.GetProperty("value").GetString());
                        break;

                    case "toggleExtra":
                        OnToggleExtra?.Invoke(root.GetProperty("value").GetString());
                        break;

                    case "setSlider":
                        OnSetSlider?.Invoke(
                            root.GetProperty("index").GetInt32(),
                            root.GetProperty("value").GetInt32()
                        );
                        break;

                    case "togglePlayfield":
                        OnTogglePlayfield?.Invoke(root.GetProperty("playfieldId").GetInt32());
                        break;

                    case "removeBounds":
                        OnRemoveBounds?.Invoke(root.GetProperty("playfieldId").GetInt32());
                        break;

                    case "startBounds":
                        OnStartBounds?.Invoke(root.GetProperty("playfieldId").GetInt32());
                        break;

                    case "confirmBounds":
                        OnConfirmBounds?.Invoke(root.GetProperty("playfieldId").GetInt32());
                        break;

                    // ── Roll list ─────────────────────────────────────────────
                    case "addRollItem":
                        var item = JsonSerializer.Deserialize<ItemEntry>(
                            root.GetProperty("item").GetRawText()
                        );
                        OnAddRollItem?.Invoke(item);
                        break;

                    case "removeRollItem":
                        OnRemoveRollItem?.Invoke(
                            root.GetProperty("lowId").GetInt32(),
                            root.GetProperty("highId").GetInt32()
                        );
                        break;

                    case "adjustRollCount":
                        OnAdjustRollCount?.Invoke(
                            root.GetProperty("lowId").GetInt32(),
                            root.GetProperty("highId").GetInt32(),
                            root.GetProperty("delta").GetInt32()
                        );
                        break;

                    // ── Roller control ────────────────────────────────────────
                    case "start":
                        OnStart?.Invoke();
                        break;

                    case "stop":
                        OnStop?.Invoke();
                        break;
                }
            }
            catch (Exception ex)
            {
                Chat.WriteLine($"HandleClient error: {ex.Message}");
                break;
            }
        }

        _clients.Remove(ws);
    }
}