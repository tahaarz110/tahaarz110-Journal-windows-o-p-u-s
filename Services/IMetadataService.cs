// ابتدای فایل: Services/IMetadataService.cs
// مسیر: /Services/IMetadataService.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingJournal.Data.Models;

namespace TradingJournal.Services
{
    public interface IMetadataService
    {
        Task<List<DynamicField>> GetFieldsAsync(string? groupName = null);
        Task<DynamicField?> GetFieldAsync(string fieldName);
        Task<DynamicField> SaveFieldAsync(DynamicField field);
        Task DeleteFieldAsync(Guid fieldId);
        
        Task<List<TabConfiguration>> GetTabsAsync();
        Task<TabConfiguration?> GetTabAsync(string tabKey);
        Task<TabConfiguration> SaveTabAsync(TabConfiguration tab);
        Task DeleteTabAsync(Guid tabId);
        
        Task<List<WidgetConfiguration>> GetWidgetsAsync(string? tabKey = null);
        Task<WidgetConfiguration?> GetWidgetAsync(string widgetKey);
        Task<WidgetConfiguration> SaveWidgetAsync(WidgetConfiguration widget);
        Task DeleteWidgetAsync(Guid widgetId);
        
        Task<string> ExportMetadataAsync();
        Task ImportMetadataAsync(string json);
        Task ResetToDefaultsAsync();
    }
}

// پایان فایل: Services/IMetadataService.cs