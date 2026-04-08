using AirlineEmpire.Models;

namespace AirlineEmpire.Services
{

/// <summary>
/// 全局游戏状态服务 — 持有所有数据，提供查询方法
/// </summary>
public class GameStateService
{
    public GameState  State  { get; private set; } = new();
    public GameConfig Config { get; private set; } = new();

    // ── 初始化 ────────────────────────────────────────────
    public void InitNewGame(GameConfig config)
    {
        Config = config;
        State  = new GameState { Funds = config.StartingFunds };

        CreateMapAirports();
    }

    /// <summary>
    /// 初始化中国地图上的机场节点（归一化坐标 0-1）
    /// MapX/MapY 对应 Canvas 的比例位置
    /// </summary>
    private void CreateMapAirports()
    {
        var airports = new[]
        {
            new Airport { Code="PEK", Name="首都国际机场",    City="北京",  MapX=0.62, MapY=0.18 },
            new Airport { Code="PKX", Name="大兴国际机场",    City="北京",  MapX=0.63, MapY=0.20 },
            new Airport { Code="SHA", Name="虹桥国际机场",    City="上海",  MapX=0.71, MapY=0.38 },
            new Airport { Code="PVG", Name="浦东国际机场",    City="上海",  MapX=0.73, MapY=0.40 },
            new Airport { Code="CAN", Name="白云国际机场",    City="广州",  MapX=0.64, MapY=0.65 },
            new Airport { Code="SZX", Name="宝安国际机场",    City="深圳",  MapX=0.65, MapY=0.67 },
            new Airport { Code="CTU", Name="天府国际机场",    City="成都",  MapX=0.42, MapY=0.52 },
            new Airport { Code="XIY", Name="咸阳国际机场",    City="西安",  MapX=0.50, MapY=0.37 },
            new Airport { Code="WUH", Name="天河国际机场",    City="武汉",  MapX=0.63, MapY=0.48 },
            new Airport { Code="HGH", Name="萧山国际机场",    City="杭州",  MapX=0.72, MapY=0.41 },
            new Airport { Code="CSX", Name="黄花国际机场",    City="长沙",  MapX=0.62, MapY=0.55 },
            new Airport { Code="NKG", Name="禄口国际机场",    City="南京",  MapX=0.69, MapY=0.36 },
            new Airport { Code="KMG", Name="长水国际机场",    City="昆明",  MapX=0.43, MapY=0.67 },
            new Airport { Code="URC", Name="地窝堡国际机场",  City="乌鲁木齐", MapX=0.17, MapY=0.22 },
            new Airport { Code="HRB", Name="太平国际机场",    City="哈尔滨", MapX=0.80, MapY=0.10 },
            new Airport { Code="XMN", Name="高崎国际机场",    City="厦门",  MapX=0.71, MapY=0.58 },
            new Airport { Code="TAO", Name="流亭国际机场",    City="青岛",  MapX=0.73, MapY=0.28 },
            new Airport { Code="TSN", Name="滨海国际机场",    City="天津",  MapX=0.65, MapY=0.20 },
        };

        foreach (var a in airports)
            State.Airports.Add(a);
    }

    // ── 查询方法 ──────────────────────────────────────────
    public Airport? GetAirport(Guid id)
        => State.Airports.FirstOrDefault(a => a.Id == id);

    public Route? GetRoute(Guid airportAId, Guid airportBId)
        => State.Routes.FirstOrDefault(r =>
            (r.AirportAId == airportAId && r.AirportBId == airportBId) ||
            (r.AirportAId == airportBId && r.AirportBId == airportAId));

    public IEnumerable<Route> GetRoutesForAirport(Guid airportId)
        => State.Routes.Where(r => r.AirportAId == airportId || r.AirportBId == airportId);

    public IEnumerable<Aircraft> GetFleetAt(Guid airportId)
        => State.Fleet.Where(a => a.CurrentAirportId == airportId && !a.IsScraped);

    /// <summary>计算两机场间距离（基于归一化坐标，换算成大致公里数）</summary>
    public double CalcDistance(Airport a, Airport b)
    {
        // 地图宽约5000km，高约3500km（中国国土）
        const double MAP_WIDTH_KM  = 5000;
        const double MAP_HEIGHT_KM = 3500;
        double dx = (a.MapX - b.MapX) * MAP_WIDTH_KM;
        double dy = (a.MapY - b.MapY) * MAP_HEIGHT_KM;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ── 操作方法 ──────────────────────────────────────────
    public (bool ok, string msg) TryOpenRoute(Airport a, Airport b)
    {
        if (GetRoute(a.Id, b.Id) != null)
            return (false, "该航线已开通");

        var route = new Route
        {
            AirportAId  = a.Id,
            AirportBId  = b.Id,
            DistanceKm  = CalcDistance(a, b),
            IsActive    = true,
        };

        if (!State.SpendMoney(route.OpeningCost))
            return (false, $"资金不足，需要 ¥{route.OpeningCost:N0}");

        State.Routes.Add(route);
        return (true, $"航线 {a.Code}↔{b.Code} 开通成功，费用 ¥{route.OpeningCost:N0}");
    }

    public (bool ok, string msg) TryUpgradeAirport(Airport airport)
    {
        if (!airport.CanUpgrade) return (false, "已达最高等级");
        if (!State.SpendMoney(airport.UpgradeCost))
            return (false, $"资金不足，需要 ¥{airport.UpgradeCost:N0}");

        airport.LevelUp();
        return (true, $"{airport.Name} 升级为 {airport.LevelName}");
    }

    public (bool ok, string msg) TryPurchaseAircraft(Airport airport, string typeCode)
    {
        if (!Aircraft.TypeConfigs.TryGetValue(typeCode, out var cfg))
            return (false, "机型不存在");

        if (airport.StationedAircraft.Count >= airport.MaxAircraftSlots)
            return (false, "停机位已满，请先升级机场");

        if (!State.SpendMoney(cfg.PurchaseCost))
            return (false, $"资金不足，需要 ¥{cfg.PurchaseCost:N0}");

        var reg = GenerateRegistration();
        var ac  = new Aircraft
        {
            Registration      = reg,
            TypeCode          = typeCode,
            CurrentAirportId  = airport.Id,
            MapX              = airport.MapX,
            MapY              = airport.MapY,
        };

        airport.StationedAircraft.Add(ac);
        State.Fleet.Add(ac);
        return (true, $"购入 {cfg.DisplayName} ({reg})");
    }

    private static int _regCounter = 1000;
    private static string GenerateRegistration()
        => $"B-{_regCounter++}";
}
}
