using System.Windows;
using System.Windows.Input;
using AirlineEmpire.Models;
using AirlineEmpire.Services;
using AirlineEmpire.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineEmpire.Views
{
    public partial class LobbyWindow : Window
    {
        private LobbyViewModel VM => (LobbyViewModel)DataContext;

        public LobbyWindow()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<LobbyViewModel>();
            VM.OnStartGame += OpenMainWindow;
        }

        private void OpenMainWindow(GameConfig cfg)
        {
            var gs = App.Services.GetRequiredService<GameStateService>();
            var engine = App.Services.GetRequiredService<SimulationEngine>();
            var export = App.Services.GetRequiredService<ExportService>();
            var mapVM = new MapViewModel(gs);
            var rankVM = new RankingViewModel(gs);
            var mainVM = new MainViewModel(gs, engine, export, mapVM, rankVM);

            var mainWin = new MainWindow(mainVM);
            mainWin.Show();
            Close();
        }

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        private void OnSelectSingle(object sender, MouseButtonEventArgs e)
            => VM.IsMultiPlayer = false;

        private void OnSelectMulti(object sender, MouseButtonEventArgs e)
            => VM.IsMultiPlayer = true;

        private void OnFundsChecked(object sender, RoutedEventArgs e)
        {
            if (DataContext == null) return;  // InitializeComponent 期间忽略
            if (sender is System.Windows.Controls.RadioButton rb &&
                rb.Tag is string tag &&
                decimal.TryParse(tag, out decimal v))
                VM.StartFunds = v;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.PasswordBox pb)
                VM.RoomPassword = pb.Password;
        }

        private void OnJoinPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.PasswordBox pb)
                VM.RoomPassword = pb.Password;
        }
    }
}