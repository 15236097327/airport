using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AirlineEmpire.Models;

namespace AirlineEmpire.Services
{

/// <summary>
/// 后端 REST API 客户端 — 房间管理 / 排行榜 / Excel 下载
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private const string BASE = "http://localhost:8080/api";

    public ApiClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    // ═══════════════════════════════════════════════════
    //  ROOM
    // ═══════════════════════════════════════════════════

    public async Task<RoomResult?> CreateRoomAsync(
        string playerName, string password, int maxPlayers = 2)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{BASE}/rooms/create", new
            {
                playerName, password, maxPlayers
            });
            if (!resp.IsSuccessStatusCode) return null;

            var api = await resp.Content.ReadFromJsonAsync<ApiResponse<RoomResult>>();
            return api?.Success == true ? api.Data : null;
        }
        catch { return null; }
    }

    public async Task<JoinResult?> JoinRoomAsync(
        string roomId, string playerName, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{BASE}/rooms/join", new
            {
                roomId, playerName, password
            });
            if (!resp.IsSuccessStatusCode) return null;

            var api = await resp.Content.ReadFromJsonAsync<ApiResponse<JoinResult>>();
            return api?.Success == true ? api.Data : null;
        }
        catch { return null; }
    }

    public async Task LeaveRoomAsync(string roomId, string playerName)
    {
        try
        {
            await _http.DeleteAsync($"{BASE}/rooms/{roomId}/leave?playerName={playerName}");
        }
        catch { /* 忽略，客户端关闭时尽力通知 */ }
    }

    // ═══════════════════════════════════════════════════
    //  LEADERBOARD
    // ═══════════════════════════════════════════════════

    public async Task<bool> SubmitRecordAsync(GameState state, GameConfig config)
    {
        try
        {
            var record = new
            {
                playerName     = config.PlayerName,
                roomId         = config.RoomId ?? "single",
                gameMode       = config.Mode.ToString(),
                finalFunds     = state.Funds,
                totalRevenue   = state.TotalRevenue,
                totalExpenses  = state.TotalExpenses,
                netProfit      = state.NetProfit,
                totalOps       = state.TotalOps,
                gameTimePlayed = state.GameTimeSeconds / 3600.0,
                isGameOver     = state.IsGameOver,
                gameOverReason = state.GameOverReason,
            };
            var resp = await _http.PostAsJsonAsync($"{BASE}/leaderboard/submit", record);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int top = 20)
    {
        try
        {
            var api = await _http.GetFromJsonAsync<ApiResponse<List<LeaderboardEntry>>>(
                $"{BASE}/leaderboard/top?n={top}");
            return api?.Data ?? [];
        }
        catch { return []; }
    }

    // ═══════════════════════════════════════════════════
    //  EXPORT — 请求服务端生成 Excel 并下载
    // ═══════════════════════════════════════════════════

    public async Task<byte[]?> DownloadExcelAsync(
        object record,
        List<object> airports,
        List<object> fleet,
        List<object> routes)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{BASE}/export/excel", new
            {
                record, airports, fleet, routes
            });
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsByteArrayAsync();
        }
        catch { return null; }
    }
}

// ── Response DTOs ──────────────────────────────────────
public class ApiResponse<T>
{
    public bool    Success { get; set; }
    public string? Message { get; set; }
    public T?      Data    { get; set; }
}

public class RoomResult
{
    public string RoomId     { get; set; } = "";
    public int    MaxPlayers { get; set; }
}

public class JoinResult
{
    public string       RoomId  { get; set; } = "";
    public List<string> Players { get; set; } = new();
    public string       Host    { get; set; } = "";
}

public class LeaderboardEntry
{
    public string  PlayerName     { get; set; } = "";
    public decimal NetProfit      { get; set; }
    public decimal FinalFunds     { get; set; }
    public int     TotalOps       { get; set; }
    public double  GameTimePlayed { get; set; }
    public string  CreatedAt      { get; set; } = "";
}
}
