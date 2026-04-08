using System.IO;
using System.Windows;
using System.Windows.Input;
using AirlineEmpire.Models;
using AirlineEmpire.Services;

namespace AirlineEmpire.Views
{

public partial class GameOverWindow : Window
{
    private readonly GameStateService _gs;
    private readonly ExportService    _export;
    private readonly ApiClient        _api = new();

    public GameOverWindow(GameStateService gs, ExportService export)
    {
        InitializeComponent();
        _gs     = gs;
        _export = export;

        PopulateSummary();
        PopulateAirports();
        _ = LoadLeaderboardAsync();
    }

    // ── 填充结算数据 ──────────────────────────────────────
    private void PopulateSummary()
    {
        var s = _gs.State;

        if (s.IsGameOver)
        {
            TxtStatus.Text       = "游戏结束";
            TxtStatus.Foreground = FindResource("AccentRedBrush") as System.Windows.Media.Brush;
            TxtReason.Text       = s.GameOverReason;
        }
        else
        {
            TxtStatus.Text       = "运营结算";
            TxtStatus.Foreground = FindResource("AccentGreenBrush") as System.Windows.Media.Brush;
            TxtReason.Text       = "恭喜完成本局游戏！";
        }

        TxtFunds.Text  = $"¥{s.Funds:N0}";
        TxtProfit.Text = $"¥{s.NetProfit:N0}";
        TxtOps.Text    = s.TotalOps.ToString();

        // 净利润变色
        TxtProfit.Foreground = s.NetProfit >= 0
            ? FindResource("AccentGreenBrush") as System.Windows.Media.Brush
            : FindResource("AccentRedBrush")   as System.Windows.Media.Brush;
    }

    private void PopulateAirports()
    {
        AirportList.ItemsSource = _gs.State.Airports
            .OrderByDescending(a => a.TotalDeparted)
            .ToList();
    }

    // ── 排行榜 ────────────────────────────────────────────
    private async Task LoadLeaderboardAsync()
    {
        var entries = await _api.GetLeaderboardAsync(20);
        var ranked  = entries.Select((e, i) => new { Rank = i + 1,
            e.PlayerName, e.NetProfit, e.CreatedAt }).ToList();
        LeaderboardList.ItemsSource = ranked;
    }

    private async void OnRefreshLeaderboard(object s, RoutedEventArgs e)
        => await LoadLeaderboardAsync();

    // ── 导出 ──────────────────────────────────────────────
    private void OnExportExcel(object s, RoutedEventArgs e)
    {
        try
        {
            var path = _export.ExportToExcel(_gs.Config.PlayerName);
            TxtExportStatus.Text = $"✓ 已保存到桌面：{System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex) { TxtExportStatus.Text = $"✗ 导出失败：{ex.Message}"; }
    }

    private void OnExportCsv(object s, RoutedEventArgs e)
    {
        try
        {
            var path = _export.ExportToCsv();
            TxtExportStatus.Text = $"✓ CSV 已保存：{System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex) { TxtExportStatus.Text = $"✗ 导出失败：{ex.Message}"; }
    }

    private async void OnUploadLeaderboard(object s, RoutedEventArgs e)
    {
        TxtExportStatus.Text = "上传中...";
        bool ok = await _api.SubmitRecordAsync(_gs.State, _gs.Config);
        TxtExportStatus.Text = ok ? "✓ 已上传排行榜！" : "✗ 上传失败（检查网络或服务器）";
        if (ok) await LoadLeaderboardAsync();
    }

    // ── 窗口控制 ──────────────────────────────────────────
    private void OnDrag(object s, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnClose(object s, RoutedEventArgs e) => Close();
}
}
