using AOSharp.Core.UI;
using Microsoft.Data.Sqlite;
using System.Net;
using System.Text;
using System.Text.Json;

public class AoDbServer
{
    private readonly string _dbPath;
    private HttpListener? _listener;
    private Thread? _serverThread;

    public AoDbServer(string dbPath)
    {
        _dbPath = dbPath;
    }

    public void Start(string url = "http://localhost:7070/")
    {
        if (!url.EndsWith("/")) url += "/";

        _serverThread = new Thread(() =>
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(url);
                _listener.Start();
                Chat.WriteLine($"AoDbServer listening on {url}");

                while (_listener.IsListening)
                {
                    try
                    {
                        var context = _listener.GetContext();
                        ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                    }
                    catch (HttpListenerException) { break; } // Stop() was called
                    catch (Exception ex) { Chat.WriteLine($"[AoDbServer] Accept error: {ex.Message}"); }
                }
            }
            catch (Exception ex)
            {
                Chat.WriteLine($"[AoDbServer] Failed to start: {ex}");
            }
        });

        _serverThread.IsBackground = true;
        _serverThread.Start();
    }

    public void Stop()
    {
        _listener?.Stop();
        _listener?.Close();
    }

    // ── Request router ────────────────────────────────────────────────────

    private void HandleRequest(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        try
        {
            // CORS
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "*");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                return;
            }

            var path = req.Url?.AbsolutePath ?? "";
            var query = req.QueryString;

            object? result = null;
            int statusCode = 200;
            string? error = null;

            if (path == "/search")
            {
                string? name = query["name"];
                int? id = int.TryParse(query["id"], out var pid) ? pid : null;
                int limit = int.TryParse(query["limit"], out var pl) ? pl : 50;
                int offset = int.TryParse(query["offset"], out var po) ? po : 0;
                result = Search(name, id, limit, offset);
            }
            else if (path == "/item")
            {
                if (!int.TryParse(query["id"], out var id)) { statusCode = 400; error = "id required"; }
                else result = GetItem(id);
            }
            else if (path == "/item/modifiers")
            {
                if (!int.TryParse(query["id"], out var id)) { statusCode = 400; error = "id required"; }
                else result = GetItemModifiers(id);
            }
            else if (path == "/item/requirements")
            {
                if (!int.TryParse(query["id"], out var id)) { statusCode = 400; error = "id required"; }
                else result = GetItemRequirements(id);
            }
            else if (path == "/items/icons")
            {
                var raw = query["ids"];
                if (string.IsNullOrEmpty(raw)) { statusCode = 400; error = "ids required"; }
                else result = GetItemIcons(raw.Split(',').Select(int.Parse).ToList());
            }
            else if (path == "/item/resolve")
            {
                if (!int.TryParse(query["lowId"], out var lowId)) { statusCode = 400; error = "lowId required"; }
                else result = ResolveItem(lowId);
            }
            else if (path == "/items/names")
            {
                var raw = query["ids"];
                if (string.IsNullOrEmpty(raw)) { statusCode = 400; error = "ids required"; }
                else result = GetItemNames(raw.Split(',').Select(int.Parse).ToList());
            }
            else
            {
                statusCode = 404;
                error = "Not found";
            }

            var json = error != null
                ? JsonSerializer.Serialize(new { error })
                : JsonSerializer.Serialize(result);

            var buffer = Encoding.UTF8.GetBytes(json);
            res.StatusCode = statusCode;
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = buffer.Length;
            res.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Chat.WriteLine($"[AoDbServer] Handler error: {ex.Message}");
            try { res.StatusCode = 500; } catch { }
        }
        finally
        {
            try { res.OutputStream.Close(); } catch { }
        }
    }

    // ── Public query methods (usable directly without HTTP) ───────────────

    public List<Dictionary<string, object?>> Search(string? name, int? id, int limit = 50, int offset = 0)
    {
        var sql = new StringBuilder(@"
            SELECT
                COALESCE(r.low_id,  i.id) AS low_id,
                COALESCE(r.high_id, i.id) AS high_id,
                COALESCE((SELECT value FROM item_stat WHERE item_id = COALESCE(r.low_id,  i.id) AND stat_id = 54), 1) AS low_ql,
                COALESCE((SELECT value FROM item_stat WHERE item_id = COALESCE(r.high_id, i.id) AND stat_id = 54), 1) AS high_ql,
                i.name, i.description,
                icon.value AS icon
            FROM item i
            LEFT JOIN item_relation r    ON r.low_id = i.id
                                        AND i.id NOT IN (SELECT high_id FROM item_relation)
            LEFT JOIN item_stat icon     ON icon.item_id = i.id AND icon.stat_id = 79
            WHERE 1=1");

        var parameters = new Dictionary<string, object>();
        if (id.HasValue) { sql.Append(" AND i.id = @id"); parameters["@id"] = id.Value; }
        else if (!string.IsNullOrEmpty(name)) { sql.Append(" AND i.name LIKE @name"); parameters["@name"] = $"%{name}%"; }
        sql.Append(" LIMIT @limit OFFSET @offset");
        parameters["@limit"] = limit;
        parameters["@offset"] = offset;
        return Query(sql.ToString(), parameters);
    }

    public List<Dictionary<string, object?>> GetItem(int id) =>
        Query("SELECT stat_id, value FROM item_stat WHERE item_id = @id",
            new Dictionary<string, object> { ["@id"] = id });

    public List<Dictionary<string, object?>> GetItemModifiers(int id) =>
        Query("SELECT event_type, function_type, list_index, operator, value_num, value_str FROM item_modifier WHERE item_id = @id",
            new Dictionary<string, object> { ["@id"] = id });

    public List<Dictionary<string, object?>> GetItemRequirements(int id) =>
        Query("SELECT outer_action_type, inner_action_type, list_index, stat, value, operator FROM item_requirement WHERE item_id = @id",
            new Dictionary<string, object> { ["@id"] = id });

    public List<Dictionary<string, object?>> GetItemIcons(List<int> idList)
    {
        var placeholders = string.Join(",", idList.Select((_, i) => $"@id{i}"));
        var parameters = idList.Select((v, i) => new { Key = $"@id{i}", Value = (object)v })
                               .ToDictionary(x => x.Key, x => x.Value);
        return Query($"SELECT item_id, value AS icon FROM item_stat WHERE stat_id = 79 AND item_id IN ({placeholders})", parameters);
    }

    public Dictionary<string, object?>? ResolveItem(int lowId)
    {
        var rows = Query(@"
            SELECT r.low_id,
                (SELECT high_id FROM item_relation WHERE low_id = r.low_id
                 ORDER BY (SELECT COALESCE(value,1) FROM item_stat WHERE item_id = item_relation.high_id AND stat_id=54) DESC
                 LIMIT 1) AS high_id,
                COALESCE(MIN(ql.value),1) AS low_ql, COALESCE(MAX(ql.value),1) AS high_ql,
                i.name, i.description, icon.value AS icon
            FROM item_relation r
            JOIN item i ON i.id = r.low_id
            LEFT JOIN item_stat ql   ON ql.item_id  = r.high_id AND ql.stat_id = 54
            LEFT JOIN item_stat icon ON icon.item_id = r.low_id  AND icon.stat_id = 79
            WHERE r.low_id = @lowId GROUP BY r.low_id LIMIT 1",
            new Dictionary<string, object> { ["@lowId"] = lowId });
        return rows.Count > 0 ? rows[0] : null;
    }

    public List<Dictionary<string, object?>> GetItemNames(List<int> idList)
    {
        var placeholders = string.Join(",", idList.Select((_, i) => $"@id{i}"));
        var parameters = idList.Select((v, i) => new { Key = $"@id{i}", Value = (object)v })
                               .ToDictionary(x => x.Key, x => x.Value);
        return Query($"SELECT id, name FROM item WHERE id IN ({placeholders})", parameters);
    }

    // ── Private ───────────────────────────────────────────────────────────

    private List<Dictionary<string, object?>> Query(string sql, Dictionary<string, object>? parameters = null)
    {
        var rows = new List<Dictionary<string, object?>>();
        using var con = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly;");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        if (parameters != null)
            foreach (var kvp in parameters)
                cmd.Parameters.AddWithValue(kvp.Key, kvp.Value);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}