// مسیر فایل: Core/Services/ServiceConfiguration.cs
// ابتدای کد
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;
using TradingJournal.Core.Services;
using TradingJournal.Core.MetadataEngine;
using TradingJournal.Core.FormEngine;
using TradingJournal.Core.QueryEngine;
using TradingJournal.Core.WidgetEngine;
using TradingJournal.Core.ReportEngine;
using TradingJournal.Core.AnalysisEngine;
using TradingJournal.Core.PluginEngine;
using TradingJournal.UI.ViewModels;
using TradingJournal.UI.Views;

namespace TradingJournal.Core.Services
{
    public static class ServiceConfiguration
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Database
            services.AddDbContext<TradingJournalContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=Data/TradingJournal.db"));

            // Repositories
            services.AddScoped<ITradeRepository, TradeRepository>();

            // Core Services
            services.AddSingleton<MetadataManager>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IMetadataService, MetadataService>();
            services.AddSingleton<BackupService>();
            services.AddSingleton<ImageManager>();
            services.AddSingleton<PluginManager>();
            
            // Engines
            services.AddTransient<DynamicFormEngine>();
            services.AddTransient<ValidationEngine>();
            services.AddTransient(typeof(DynamicQueryBuilder<>));
            services.AddTransient<QueryRepository>();
            services.AddTransient<ExportManager>();
            services.AddTransient<DynamicWidgetEngine>();
            services.AddTransient<DynamicReportEngine>();
            services.AddTransient<SmartAnalysisEngine>();

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();

            // Views
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardView>();
            services.AddTransient<TradeListView>();
            services.AddTransient<FormDesigner>();
            services.AddTransient<QueryBuilder>();
            services.AddTransient<SettingsView>();
            services.AddTransient<ReportView>();
            services.AddTransient<PluginManagerView>();
            services.AddTransient<DynamicFormView>();

            // Configuration
            services.AddSingleton<IConfiguration>(configuration);

            return services;
        }
    }
}
// پایان کد