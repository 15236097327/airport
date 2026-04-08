using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AirlineEmpire.Converters;
using AirlineEmpire.Services;
using AirlineEmpire.ViewModels;
using AirlineEmpire.Views;

namespace AirlineEmpire
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 先注册服务
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            Services = collection.BuildServiceProvider();

            // 2. 再注册 Converter（Services 已就绪）
            Resources.Add("BoolToVisibility", new BoolToVisibilityConverter { Invert = false });
            Resources.Add("InvertBoolToVisibility", new BoolToVisibilityConverter { Invert = true });
            Resources.Add("UrgencyToColor", new UrgencyToColorConverter());
            Resources.Add("WearToColor", new WearToColorConverter());
            Resources.Add("RatioToWidth", new RatioToWidthConverter());
            Resources.Add("RatioToFixedWidth", new RatioToFixedWidthConverter());
            Resources.Add("BoolToUpgradeText", new BoolToUpgradeTextConverter());
            Resources.Add("CurrencyFormat", new CurrencyConverter());
            Resources.Add("LogLevelToColor", new LogLevelToColorConverter());

            // 3. 最后才创建并显示窗口
            var lobby = new LobbyWindow();
            lobby.Show();
        }

        private static void ConfigureServices(IServiceCollection s)
        {
            s.AddSingleton<GameStateService>();
            s.AddSingleton<SimulationEngine>();
            s.AddSingleton<PassengerSpawner>();
            s.AddSingleton<FlightDispatcher>();
            s.AddSingleton<ConflictMonitor>();
            s.AddSingleton<ExportService>();

            s.AddTransient<LobbyViewModel>();
            s.AddTransient<MainViewModel>();
            s.AddTransient<MapViewModel>();
            s.AddTransient<RankingViewModel>();
        }
    }
}