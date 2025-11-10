// مسیر فایل: MainWindow.xaml.cs
// ابتدای کد
using System;
using System.Windows;
using System.Windows.Threading;
using TradingJournal.Core.Services;
using TradingJournal.UI.ViewModels;

namespace TradingJournal
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly INavigationService _navigationService;
        private readonly DispatcherTimer _timer;

        public MainWindow(MainViewModel viewModel, INavigationService navigationService)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            _navigationService = navigationService;
            DataContext = _viewModel;

            // Initialize navigation
            _navigationService.Initialize(MainFrame);

            // Setup timer for clock
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += UpdateClock;
            _timer.Start();

            // Load initial view
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Navigate to default view
            _navigationService.NavigateTo("DashboardView");
        }

        private void UpdateClock(object sender, EventArgs e)
        {
            _viewModel.CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}
// پایان کد