using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AirlineEmpire.Models;

namespace AirlineEmpire.Services
{

/// <summary>
/// 多人模式 WebSocket 客户端 — 连接 Spring Boot 后端
/// 使用 STOMP over WebSocket 协议
/// </summary>
public class WebSocketService : IDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private string _roomId = "";
    private string _playerName = "";

    // 服务端地址（开发时）
    private const string SERVER_URL = "ws://localhost:8080/ws/game/websocket";

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    // ── 事件 ──────────────────────────────────────────────
    public event Action<GameSyncMessage>? OnMessageReceived;
    public event Action?                 OnConnected;
    public event Action<string>?         OnDisconnected;
    public event Action<string>?         OnError;

    // ── 连接 ──────────────────────────────────────────────
    public async Task ConnectAsync(string roomId, string playerName)
    {
        _roomId    = roomId;
        _playerName= playerName;
        _cts       = new CancellationTokenSource();
        _ws        = new ClientWebSocket();

        try
        {
            await _ws.ConnectAsync(new Uri(SERVER_URL), _cts.Token);

            // STOMP CONNECT 握手
            await SendRawAsync(BuildStompConnect());

            // STOMP SUBSCRIBE：订阅房间频道
            await SendRawAsync(BuildStompSubscribe($"/topic/room/{roomId}"));

            OnConnected?.Invoke();

            // 启动接收循环
            _ = Task.Run(ReceiveLoopAsync, _cts.Token);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"连接失败: {ex.Message}");
        }
    }

    // ── 发送操作 ──────────────────────────────────────────
    public async Task SendActionAsync(string actionType, object payload)
    {
        if (!IsConnected) return;

        var msg = new
        {
            actionType,
            playerName = _playerName,
            payload    = JsonSerializer.Serialize(payload)
        };

        var stompMsg = BuildStompSend(
            $"/app/room/{_roomId}/action",
            JsonSerializer.Serialize(msg));

        await SendRawAsync(stompMsg);
    }

    public async Task SendGameOverAsync(string reason)
    {
        if (!IsConnected) return;
        await SendRawAsync(BuildStompSend(
            $"/app/room/{_roomId}/gameover", reason));
    }

    // ── 接收循环 ──────────────────────────────────────────
    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[4096];
        var sb     = new StringBuilder();

        while (_ws!.State == WebSocketState.Open && !_cts!.IsCancellationRequested)
        {
            try
            {
                var result = await _ws.ReceiveAsync(buffer, _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    OnDisconnected?.Invoke("服务器关闭连接");
                    break;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage) continue;

                var raw = sb.ToString();
                sb.Clear();

                ParseStompFrame(raw);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                OnError?.Invoke($"接收错误: {ex.Message}");
                break;
            }
        }
    }

    // ── STOMP 帧解析 ──────────────────────────────────────
    private void ParseStompFrame(string frame)
    {
        if (!frame.StartsWith("MESSAGE")) return;

        // 提取 body（STOMP 帧头尾用 \0 分隔，\n\n 分隔头和体）
        int bodyStart = frame.IndexOf("\n\n", StringComparison.Ordinal);
        if (bodyStart < 0) return;
        string body = frame[(bodyStart + 2)..].TrimEnd('\0');

        try
        {
            var msg = JsonSerializer.Deserialize<GameSyncMessage>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (msg != null)
                OnMessageReceived?.Invoke(msg);
        }
        catch { /* 忽略非 JSON 消息 */ }
    }

    // ── STOMP 帧构建 ──────────────────────────────────────
    private static string BuildStompConnect() =>
        "CONNECT\naccept-version:1.2\nheart-beat:0,0\n\n\0";

    private static string BuildStompSubscribe(string dest) =>
        $"SUBSCRIBE\nid:sub-0\ndestination:{dest}\n\n\0";

    private static string BuildStompSend(string dest, string body) =>
        $"SEND\ndestination:{dest}\ncontent-type:application/json\n\n{body}\0";

    // ── 底层发送 ──────────────────────────────────────────
    private async Task SendRawAsync(string text)
    {
        if (_ws?.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts!.Token);
    }

    // ── 断开 ──────────────────────────────────────────────
    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _ws?.Dispose();
        _cts?.Dispose();
    }
}

/// <summary>服务端推送消息结构</summary>
public class GameSyncMessage
{
    public string SyncType   { get; set; } = "";  // STATE_UPDATE | PLAYER_JOIN | GAME_OVER ...
    public string RoomId     { get; set; } = "";
    public string FromPlayer { get; set; } = "";
    public string Payload    { get; set; } = "";
    public long   Timestamp  { get; set; }
}
}
