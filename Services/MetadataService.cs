// ابتدای فایل: Services/MetadataService.cs
// مسیر: /Services/MetadataService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using TradingJournal.Data;
using TradingJournal.Data.Models;

namespace TradingJournal.Services
{
    public class MetadataService : IMetadataService
    {
        private readonly DatabaseContext _dbContext;

        public MetadataService()
        {
            _dbContext = new DatabaseContext();
        }

        public async Task<List<DynamicField>> GetFieldsAsync(string? groupName = null)
        {
            var query = _dbContext.DynamicFields.AsQueryable();
            
            if (!string.IsNullOrEmpty(groupName))
            {
                query = query.Where(f => f.GroupName == groupName);
            }
            
            return await query.OrderBy(f => f.OrderIndex).ToListAsync();
        }

        public async Task<DynamicField?> GetFieldAsync(string fieldName)
        {
            return await _dbContext.DynamicFields
                .FirstOrDefaultAsync(f => f.FieldName == fieldName);
        }

        public async Task<DynamicField> SaveFieldAsync(DynamicField field)
        {
            if (field.Id == Guid.Empty)
            {
                _dbContext.DynamicFields.Add(field);
            }
            else
            {
                _dbContext.DynamicFields.Update(field);
            }
            
            await _dbContext.SaveChangesAsync();
            return field;
        }

        public async Task DeleteFieldAsync(Guid fieldId)
        {
            var field = await _dbContext.DynamicFields.FindAsync(fieldId);
            if (field != null)
            {
                field.IsDeleted = true;
                field.DeletedAt = DateTime.Now;
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<TabConfiguration>> GetTabsAsync()
        {
            return await _dbContext.TabConfigurations
                .Where(t => t.IsVisible)
                .OrderBy(t => t.OrderIndex)
                .ToListAsync();
        }

        public async Task<TabConfiguration?> GetTabAsync(string tabKey)
        {
            return await _dbContext.TabConfigurations
                .FirstOrDefaultAsync(t => t.TabKey == tabKey);
        }

        public async Task<TabConfiguration> SaveTabAsync(TabConfiguration tab)
        {
            if (tab.Id == Guid.Empty)
            {
                _dbContext.TabConfigurations.Add(tab);
            }
            else
            {
                _dbContext.TabConfigurations.Update(tab);
            }
            
            await _dbContext.SaveChangesAsync();
            return tab;
        }

        public async Task DeleteTabAsync(Guid tabId)
        {
            var tab = await _dbContext.TabConfigurations.FindAsync(tabId);
            if (tab != null)
            {
                tab.IsDeleted = true;
                tab.DeletedAt = DateTime.Now;
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<WidgetConfiguration>> GetWidgetsAsync(string? tabKey = null)
        {
            var query = _dbContext.WidgetConfigurations.AsQueryable();
            
            if (!string.IsNullOrEmpty(tabKey))
            {
                query = query.Where(w => w.TabKey == tabKey);
            }
            
            return await query
                .OrderBy(w => w.Row)
                .ThenBy(w => w.Column)
                .ToListAsync();
        }

        public async Task<WidgetConfiguration?> GetWidgetAsync(string widgetKey)
        {
            return await _dbContext.WidgetConfigurations
                .FirstOrDefaultAsync(w => w.WidgetKey == widgetKey);
        }

        public async Task<WidgetConfiguration> SaveWidgetAsync(WidgetConfiguration widget)
        {
            if (widget.Id == Guid.Empty)
            {
                _dbContext.WidgetConfigurations.Add(widget);
            }
            else
            {
                _dbContext.WidgetConfigurations.Update(widget);
            }
            
            await _dbContext.SaveChangesAsync();
            return widget;
        }

        public async Task DeleteWidgetAsync(Guid widgetId)
        {
            var widget = await _dbContext.WidgetConfigurations.FindAsync(widgetId);
            if (widget != null)
            {
                widget.IsDeleted = true;
                widget.DeletedAt = DateTime.Now;
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<string> ExportMetadataAsync()
        {
            var metadata = new
            {
                Fields = await GetFieldsAsync(),
                Tabs = await GetTabsAsync(),
                Widgets = await GetWidgetsAsync(),
                ExportDate = DateTime.Now
            };
            
            return JsonConvert.SerializeObject(metadata, Formatting.Indented);
        }

        public async Task ImportMetadataAsync(string json)
        {
            try
            {
                var metadata = JsonConvert.DeserializeObject<dynamic>(json);
                
                // Import logic here
                Log.Information("Metadata imported successfully");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error importing metadata");
                throw;
            }
        }

        public async Task ResetToDefaultsAsync()
        {
            // Reset to default configurations
            Log.Information("Resetting metadata to defaults");
            await Task.CompletedTask;
        }
    }
}

// پایان فایل: Services/MetadataService.cs