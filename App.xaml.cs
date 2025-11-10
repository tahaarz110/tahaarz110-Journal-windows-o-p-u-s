// مسیر فایل: App.xaml.cs
// ابتدای کد
using System;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradingJournal.Core.Services;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;
using TradingJournal.Core.MetadataEngine;
using TradingJournal.Core.FormEngine;
using TradingJournal.Core.QueryEngine;
using TradingJournal.Core.AnalysisEngine;
using TradingJournal.UI.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace TradingJournal
{
    public partial class App : Application
    {
        private IHost _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Setup exception handling
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // Build host
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            // Initialize database
            await InitializeDatabase();

            // Start host
            await _host.StartAsync();

            // Show main window
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Database
            services.AddDbContext<TradingJournalContext>(options =>
                options.UseSqlite("Data Source=Data/TradingJournal.db"));

            // Repositories
            services.AddScoped<ITradeRepository, TradeRepository>();

            // Core Services
            services.AddSingleton<MetadataManager>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<BackupService>();
            services.AddSingleton<ImageManager>();
            services.AddSingleton<SmartAnalysisEngine>();
            services.AddSingleton<ExportManager>();

            // Form Engine
            services.AddTransient<DynamicFormEngine>();
            services.AddTransient<ValidationEngine>();

            // Query Engine
            services.AddTransient(typeof(DynamicQueryBuilder<>));
            services.AddTransient<QueryRepository>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            // Views
            services.AddSingleton<MainWindow>();

            // Configuration
            services.AddSingleton<IConfiguration>(provider =>
            {
                var config = new Configuration();
                config.Load();
                return config;
            });

            // Theme Service
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IMetadataService, MetadataService>();
        }

        private async Task InitializeDatabase()
        {
            using var scope = _host.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TradingJournalContext>();
            
            // Ensure database is created
            await context.Database.EnsureCreatedAsync();
            
            // Run migrations if any
            if (context.Database.GetPendingMigrations().Any())
            {
                await context.Database.MigrateAsync();
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            // Save settings
            var config = _host.Services.GetRequiredService<IConfiguration>();
            config.Save();

            // Stop host
            await _host.StopAsync();
            _host.Dispose();

            base.OnExit(e);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            LogError(exception);
            
            MessageBox.Show(
                $"خطای غیرمنتظره رخ داد:\n{exception?.Message}",
                "خطا",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogError(e.Exception);
            e.SetObserved();
        }

        private void LogError(Exception exception)
        {
            // Log to file
            var logPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Logs",
                $"error_{DateTime.Now:yyyyMMdd}.log");

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
            
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception?.ToString()}\n\n";
            System.IO.File.AppendAllText(logPath, logMessage);
        }
    }
}
// پایان کد