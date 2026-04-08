using System.IO;
using System.Text;
using ClosedXML.Excel;
using AirlineEmpire.Models;

namespace AirlineEmpire.Services
{

public class ExportService
{
    private readonly GameStateService _gs;

    public ExportService(GameStateService gs) => _gs = gs;

    // ── Excel 导出 ────────────────────────────────────────
    public string ExportToExcel(string playerName)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"AirlineEmpire_{playerName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");

        using var wb = new XLWorkbook();

        CreateSummarySheet(wb, playerName);
        CreateAirportSheet(wb);
        CreateFleetSheet(wb);
        CreateRouteSheet(wb);

        wb.SaveAs(path);
        return path;
    }

    private void CreateSummarySheet(XLWorkbook wb, string playerName)
    {
        var ws = wb.Worksheets.Add("总览");
        var s  = _gs.State;

        ws.Cell("A1").Value = "航空帝国 — 游戏结算报告";
        ws.Cell("A1").Style.Font.Bold      = true;
        ws.Cell("A1").Style.Font.FontSize  = 16;

        var rows = new (string label, string val)[]
        {
            ("玩家",       playerName),
            ("游戏时长",   $"{s.GameTimeSeconds/3600:F1} 小时"),
            ("最终资金",   $"¥{s.Funds:N0}"),
            ("累计收益",   $"¥{s.TotalRevenue:N0}"),
            ("累计支出",   $"¥{s.TotalExpenses:N0}"),
            ("净利润",     $"¥{s.NetProfit:N0}"),
            ("总起降次数", s.TotalOps.ToString()),
            ("结束原因",   s.IsGameOver ? s.GameOverReason : "正常结束"),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            ws.Cell(i + 3, 1).Value = rows[i].label;
            ws.Cell(i + 3, 2).Value = rows[i].val;
        }

        ws.Column(1).Width = 18;
        ws.Column(2).Width = 25;
    }

    private void CreateAirportSheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("机场数据");
        var headers = new[]
        {
            "代码","名称","城市","最终等级","等级名称",
            "当前滞留量","最大容纳量","历史最高滞留","累计出发","累计抵达","累计收益"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.DarkBlue;
            ws.Cell(1, i + 1).Style.Font.FontColor       = XLColor.White;
        }

        int row = 2;
        foreach (var a in _gs.State.Airports)
        {
            ws.Cell(row, 1).Value  = a.Code;
            ws.Cell(row, 2).Value  = a.Name;
            ws.Cell(row, 3).Value  = a.City;
            ws.Cell(row, 4).Value  = a.Level;
            ws.Cell(row, 5).Value  = a.LevelName;
            ws.Cell(row, 6).Value  = a.CurrentPassengers;
            ws.Cell(row, 7).Value  = a.MaxCapacity;
            ws.Cell(row, 8).Value  = a.HistoricalMaxPax;
            ws.Cell(row, 9).Value  = a.TotalDeparted;
            ws.Cell(row, 10).Value = a.TotalArrived;
            ws.Cell(row, 11).Value = (double)a.TotalRevenue;
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private void CreateFleetSheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("机队数据");
        var headers = new[]
        {
            "注册号","机型","当前状态","磨损度%","总飞行次数","运客总量","总收益"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.DarkGreen;
            ws.Cell(1, i + 1).Style.Font.FontColor       = XLColor.White;
        }

        int row = 2;
        foreach (var ac in _gs.State.Fleet)
        {
            ws.Cell(row, 1).Value = ac.Registration;
            ws.Cell(row, 2).Value = ac.Config.DisplayName;
            ws.Cell(row, 3).Value = ac.StatusText;
            ws.Cell(row, 4).Value = Math.Round(ac.WearPercent, 1);
            ws.Cell(row, 5).Value = ac.TotalFlights;
            ws.Cell(row, 6).Value = ac.TotalPassengersCarried;
            ws.Cell(row, 7).Value = (double)ac.TotalRevenue;
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private void CreateRouteSheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("航线数据");
        var headers = new[]
        {
            "航线","距离(km)","运营次数","总收益","总成本","净利润"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.DarkRed;
            ws.Cell(1, i + 1).Style.Font.FontColor       = XLColor.White;
        }

        int row = 2;
        foreach (var r in _gs.State.Routes)
        {
            var a = _gs.GetAirport(r.AirportAId);
            var b = _gs.GetAirport(r.AirportBId);
            ws.Cell(row, 1).Value = $"{a?.Code}↔{b?.Code}";
            ws.Cell(row, 2).Value = Math.Round(r.DistanceKm);
            ws.Cell(row, 3).Value = r.TotalFlightsOperated;
            ws.Cell(row, 4).Value = (double)r.TotalRevenue;
            ws.Cell(row, 5).Value = (double)r.TotalCost;
            ws.Cell(row, 6).Value = (double)r.Profit;
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    // ── CSV 导出（轻量版） ─────────────────────────────────
    public string ExportToCsv()
    {
        var sb  = new StringBuilder();
        var path= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"AirlineEmpire_{DateTime.Now:yyyyMMdd_HHmm}.csv");

        sb.AppendLine("代码,名称,等级,当前滞留,最大容量,历史最高,出发总量,抵达总量");
        foreach (var a in _gs.State.Airports)
            sb.AppendLine($"{a.Code},{a.Name},{a.Level}," +
                          $"{a.CurrentPassengers},{a.MaxCapacity}," +
                          $"{a.HistoricalMaxPax},{a.TotalDeparted},{a.TotalArrived}");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }
}
}
