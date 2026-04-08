using System.Windows;
using System.Windows.Input;
using AirlineEmpire.ViewModels;

namespace AirlineEmpire.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            vm.RankingVM.OnJumpToAirport += airport =>
            {
                vm.MapVM.SelectedAirport = airport;
                vm.MapVM.RaiseAirportSelected(airport);
            };
        }

        private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (DataContext is not MainViewModel vm) return;
            switch (e.Key)
            {
                case Key.Space: vm.TogglePauseCommand.Execute(null); break;
                case Key.D1:    vm.SetSpeedCommand.Execute("1");     break;
                case Key.D2:    vm.SetSpeedCommand.Execute("2");     break;
                case Key.D3:    vm.SetSpeedCommand.Execute("5");     break;
                case Key.D4:    vm.SetSpeedCommand.Execute("10");    break;
                case Key.Escape:vm.CloseAirportPanelCommand.Execute(null); break;
            }
        }
    }
}
