// 📁 SDK/PluginTemplate.cs
// ===== شروع کد =====

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingJournal.Core.PluginEngine;

namespace TradingJournal.SDK
{
    /// <summary>
    /// کلاس پایه برای توسعه آسان‌تر پلاگین‌ها
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        protected IPluginContext Context { get; private set; }
        
        // Abstract properties که باید override شوند
        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract Version Version { get; }
        public abstract string Author { get; }
        
        // Virtual properties با مقادیر پیش‌فرض
        public virtual string IconName => "Package";
        public virtual PluginCategory Category => PluginCategory.Utility;
        public virtual IEnumerable<PluginPermission> RequiredPermissions => new PluginPermission[0];
        
        // Lifecycle methods
        public virtual async Task<bool> InitializeAsync(IPluginContext context)
        {
            Context = context;
            Context.Logger.LogInfo($"Initializing {Name} v{Version}");
            
            try
            {
                await OnInitializeAsync();
                Context.Logger.LogInfo($"{Name} initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Context.Logger.LogError($"Failed to initialize {Name}", ex);
                return false;
            }
        }
        
        public virtual async Task ShutdownAsync()
        {
            Context.Logger.LogInfo($"Shutting down {Name}");
            await OnShutdownAsync();
        }
        
        // Abstract methods برای پیاده‌سازی
        protected abstract Task OnInitializeAsync();
        protected abstract Task OnShutdownAsync();
        public abstract Task<PluginResult> ExecuteAsync(PluginRequest request);
        
        // Helper methods
        protected PluginResult Success(object data = null, string message = "عملیات با موفقیت انجام شد")
        {
            return new PluginResult
            {
                Success = true,
                Message = message,
                Data = data
            };
        }
        
        protected PluginResult Error(string message, Exception ex = null)
        {
            var errors = new List<PluginError>
            {
                new PluginError
                {
                    Message = message,
                    Details = ex?.ToString(),
                    Severity = ErrorSeverity.Error
                }
            };
            
            return new PluginResult
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }
        
        protected PluginResult TableResult(object tableData)
        {
            return new PluginResult
            {
                Success = true,
                ResultType = PluginResultType.Table,
                Data = tableData
            };
        }
        
        protected PluginResult ChartResult(object chartData)
        {
            return new PluginResult
            {
                Success = true,
                ResultType = PluginResultType.Chart,
                Data = chartData
            };
        }
        
        // Default implementations
        public virtual IEnumerable<PluginWidget> GetWidgets()
        {
            return new PluginWidget[0];
        }
        
        public virtual IEnumerable<PluginSetting> GetSettings()
        {
            return new PluginSetting[0];
        }
        
        public virtual async Task HandleEventAsync(PluginEvent pluginEvent)
        {
            // Override در پلاگین‌هایی که نیاز به handle event دارند
        }
    }
}

// ===== پایان کد =====