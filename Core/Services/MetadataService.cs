// مسیر فایل: Core/Services/MetadataService.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingJournal.Core.MetadataEngine;
using TradingJournal.Core.MetadataEngine.Models;

namespace TradingJournal.Core.Services
{
    public class MetadataService : IMetadataService
    {
        private readonly MetadataManager _metadataManager;

        public MetadataService(MetadataManager metadataManager)
        {
            _metadataManager = metadataManager;
        }

        public async Task<List<TabMetadata>> GetAllTabsAsync()
        {
            return await Task.Run(() => _metadataManager.GetAllTabs());
        }

        public async Task<TabMetadata> GetTabAsync(string name)
        {
            return await _metadataManager.LoadTabMetadataAsync(name);
        }

        public async Task SaveTabAsync(TabMetadata tab)
        {
            await _metadataManager.SaveTabMetadataAsync(tab);
        }

        public async Task<List<WidgetMetadata>> GetAllWidgetsAsync()
        {
            return await Task.Run(() => _metadataManager.GetAllWidgets());
        }

        public async Task<WidgetMetadata> GetWidgetAsync(string name)
        {
            return await _metadataManager.LoadWidgetMetadataAsync(name);
        }

        public async Task SaveWidgetAsync(WidgetMetadata widget)
        {
            await _metadataManager.SaveWidgetMetadataAsync(widget);
        }

        public async Task<FormMetadata> GetFormAsync(string name)
        {
            return await _metadataManager.LoadFormMetadataAsync(name);
        }

        public async Task SaveFormAsync(FormMetadata form)
        {
            await _metadataManager.SaveFormMetadataAsync(form);
        }

        public async Task<List<string>> GetAllFormNamesAsync()
        {
            return await Task.Run(() => _metadataManager.GetAllFormNames());
        }
    }
}
// پایان کد