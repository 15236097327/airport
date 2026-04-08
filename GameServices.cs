using AirlineEmpire.Models;

namespace AirlineEmpire.Services
{

// ═══════════════════════════════════════════════════════
//  PASSENGER SPAWNER — 旅客生成（泊松分布）
// ═══════════════════════════════════════════════════════
public class PassengerSpawner
{
    private readonly GameStateService _gs;
    private readonly Random           _rng = new();

    // 各机场基础客流系数（越大城市越多人）
    private static readonly Dictionary<string, double> TrafficWeight = new()
    {
        ["PEK"]=2.0, ["PKX"]=1.8, ["SHA"]=2.0, ["PVG"]=2.2,
        ["CAN"]=1.8, ["SZX"]=1.7, ["CTU"]=1.5, ["XIY"]=1.2,
        ["WUH"]=1.3, ["HGH"]=1.4, ["CSX"]=1.1, ["NKG"]=1.3,
        ["KMG"]=1.0, ["URC"]=0.8, ["HRB"]=0.9, ["XMN"]=1.0,
        ["TAO"]=1.0, ["TSN"]=1.1,
    };

    public PassengerSpawner(GameStateService gs) => _gs = gs;

    public void SpawnPassengers(double gameDtSeconds)
    {
        var airports = _gs.State.Airports;
        if (airports.Count < 2) return;

        foreach (var airport in airports)
        {
            if (airport.IsFull) continue;

            double weight = TrafficWeight.GetValueOrDefault(airport.Code, 1.0);
            // 基础每分钟到达旅客数
            double arrivalRate = weight * 2.0 * (gameDtSeconds / 60.0);
            int count = PoissonSample(arrivalRate);

            var newPax = new List<Passenger>();
            for (int i = 0; i < count; i++)
            {
                if (airport.CurrentPassengers + newPax.Count >= airport.MaxCapacity)
                    break;

                // 随机选目的地（优先有航线的机场）
                var dest = PickDestination(airport);
                if (dest == null) continue;

                newPax.Add(new Passenger
                {
                    OriginAirportId      = airport.Id,
                    DestinationAirportId = dest.Id,
                });
            }

            if (newPax.Count > 0)
                airport.AddPassengers(newPax);
        }

        // 更新旅客等待时间
        foreach (var airport in airports)
            foreach (var q in airport.PassengerGroups.Values)
                foreach (var p in q)
                    p.WaitingSeconds += gameDtSeconds;
    }

    private Airport? PickDestination(Airport origin)
    {
        var routes = _gs.GetRoutesForAirport(origin.Id).ToList();

        // 有航线时70%概率选有航线的目的地，30%随机
        if (routes.Count > 0 && _rng.NextDouble() < 0.7)
        {
            var route = routes[_rng.Next(routes.Count)];
            return _gs.GetAirport(route.OtherAirport(origin.Id));
        }

        var others = _gs.State.Airports.Where(a => a.Id != origin.Id).ToList();
        return others.Count > 0 ? others[_rng.Next(others.Count)] : null;
    }

    private int PoissonSample(double lambda)
    {
        if (lambda <= 0) return 0;
        double L = Math.Exp(-lambda);
        double p = 1.0;
        int k = 0;
        do { k++; p *= _rng.NextDouble(); } while (p > L);
        return k - 1;
    }
}

// ═══════════════════════════════════════════════════════
//  FLIGHT DISPATCHER — 航班派遣（动态派遣策略）
// ═══════════════════════════════════════════════════════
public class FlightDispatcher
{
    private readonly GameStateService _gs;

    // 派遣阈值：某目的地旅客 >= 飞机座位数 * THRESHOLD 时触发
    private const double DISPATCH_THRESHOLD = 0.70;
    // 超时强制起飞：旅客等待超过此时间(游戏秒)强制派遣
    private const double FORCE_DISPATCH_WAIT = 1800; // 30分钟

    public FlightDispatcher(GameStateService gs) => _gs = gs;

    public void Tick(double gameDtSeconds, Action<string> log)
    {
        foreach (var airport in _gs.State.Airports)
        {
            TryDispatchFrom(airport, log);
        }
    }

    private void TryDispatchFrom(Airport origin, Action<string> log)
    {
        // 找可用飞机（在本机场待命、未报废）
        var availAircraft = origin.StationedAircraft
            .Where(a => a.Status == AircraftStatus.Idle && !a.IsScraped)
            .ToList();

        if (availAircraft.Count == 0) return;

        foreach (var (destId, queue) in origin.PassengerGroups.ToList())
        {
            if (queue.Count == 0) continue;

            // 检查是否有该目的地的航线
            var route = _gs.GetRoute(origin.Id, destId);
            if (route == null) continue;

            var dest = _gs.GetAirport(destId);
            if (dest == null) continue;

            // 找合适飞机
            var ac = availAircraft.FirstOrDefault(a => a.Status == AircraftStatus.Idle);
            if (ac == null) break;

            bool shouldDispatch =
                queue.Count >= (int)(ac.Seats * DISPATCH_THRESHOLD) ||
                queue.Any(p => p.WaitingSeconds >= FORCE_DISPATCH_WAIT);

            if (!shouldDispatch) continue;

            // 装载旅客
            var boarded = origin.BoardPassengers(destId, ac.Seats);
            if (boarded.Count == 0) continue;

            ac.OnboardPassengers.AddRange(boarded);

            // 计算飞行时长（游戏秒）
            double flightSecs = (route.DistanceKm / ac.Config.SpeedKmH) * 3600;

            // 计算收益与成本
            decimal ticketRev  = ac.CalcTicketRevenue(route.DistanceKm);
            decimal fuelCost   = ac.CalcFuelCost(route.DistanceKm);

            var flight = new Flight
            {
                Aircraft            = ac,
                Route               = route,
                FromId              = origin.Id,
                ToId                = destId,
                TotalDurationSeconds= flightSecs,
                Status              = FlightStatus.InFlight,
                TicketRevenue       = ticketRev,
                FuelCost            = fuelCost,
            };

            // 更新状态
            ac.Status              = AircraftStatus.Flying;
            ac.DestinationAirportId= destId;
            origin.StationedAircraft.Remove(ac);

            _gs.State.Flights.Add(flight);
            _gs.State.EarnMoney(ticketRev);
            _gs.State.SpendMoney(fuelCost);

            route.TotalFlightsOperated++;
            route.TotalRevenue += ticketRev;
            route.TotalCost    += fuelCost;
            origin.TotalDeparted += boarded.Count;
            _gs.State.TotalOps++;

            availAircraft.Remove(ac);
            log($"✈ {ac.Registration} {origin.Code}→{dest.Code}  {boarded.Count}人  收益¥{ticketRev:N0}");
        }
    }

    public void HandleArrival(Flight flight, Action<string> log)
    {
        var ac   = flight.Aircraft;
        var dest = _gs.GetAirport(flight.ToId)!;

        // 卸客
        int paxCount = ac.OnboardPassengers.Count;
        dest.TotalArrived += paxCount;
        ac.OnboardPassengers.Clear();

        // 飞机落地
        ac.Status             = AircraftStatus.Idle;
        ac.CurrentAirportId   = dest.Id;
        ac.DestinationAirportId = null;
        ac.ProgressRatio      = 0;
        ac.MapX               = dest.MapX;
        ac.MapY               = dest.MapY;
        ac.ApplyWear();

        dest.StationedAircraft.Add(ac);
        flight.Status = FlightStatus.Arrived;
        _gs.State.Flights.Remove(flight);

        // 检查报废
        if (ac.IsScraped)
        {
            dest.StationedAircraft.Remove(ac);
            log($"🔧 {ac.Registration} 已达使用年限，自动报废");
        }
        else
        {
            log($"🛬 {ac.Registration} 抵达 {dest.Code}，载客{paxCount}人");
        }
    }
}

// ═══════════════════════════════════════════════════════
//  CONFLICT MONITOR — 满员报警与游戏结束判定
// ═══════════════════════════════════════════════════════
public class ConflictMonitor
{
    private readonly GameStateService _gs;

    public ConflictMonitor(GameStateService gs) => _gs = gs;

    public void Tick(double gameDt, Action<string> log, Action<string> onGameOver)
    {
        foreach (var airport in _gs.State.Airports)
        {
            if (airport.IsFull)
            {
                if (!airport.IsAlarming)
                {
                    airport.IsAlarming           = true;
                    airport.AlarmTimeRemaining   = _gs.Config.AlarmGraceSecs;
                    log($"⚠ {airport.Code} 旅客爆满！{_gs.Config.AlarmGraceSecs}秒内未处理将结束游戏");
                }
                else
                {
                    airport.AlarmTimeRemaining -= gameDt;
                    if (airport.AlarmTimeRemaining <= 0)
                    {
                        onGameOver($"{airport.Name}({airport.Code}) 旅客严重积压，运营崩溃");
                        return;
                    }
                }
            }
            else
            {
                airport.IsAlarming         = false;
                airport.AlarmTimeRemaining = 0;
            }
        }
    }
}
}
