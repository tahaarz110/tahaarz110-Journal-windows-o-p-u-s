// مسیر فایل: Core/MetadataEngine/Models/FormMetadata.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using TradingJournal.Core.FormEngine.Models;

namespace TradingJournal.Core.MetadataEngine.Models
{
    public class FormMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public List<DynamicField> Fields { get; set; }
        public FormSettings Settings { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string Version { get; set; }
        public bool IsActive { get; set; }

        public FormMetadata()
        {
            Id = Guid.NewGuid().ToString();
            Fields = new List<DynamicField>();
            Settings = new FormSettings();
            CustomProperties = new Dictionary<string, object>();
            CreatedDate = DateTime.Now;
            Version = "1.0.0";
            IsActive = true;
        }
    }

    public class FormSettings
    {
        public bool ShowTitle { get; set; } = true;
        public bool ShowDescription { get; set; } = true;
        public bool AutoSave { get; set; } = false;
        public int AutoSaveInterval { get; set; } = 60; // seconds
        public bool RequireConfirmation { get; set; } = true;
        public string SubmitButtonText { get; set; } = "ذخیره";
        public string CancelButtonText { get; set; } = "انصراف";
        public FormLayout Layout { get; set; } = FormLayout.Vertical;
        public int ColumnsCount { get; set; } = 1;
        public bool EnableValidation { get; set; } = true;
        public bool ShowProgressBar { get; set; } = false;
    }

    public enum FormLayout
    {
        Vertical,
        Horizontal,
        Grid,
        Tabbed
    }

    public class TabMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public string Icon { get; set; }
        public TabType Type { get; set; }
        public string ContentSource { get; set; } // Form name, View name, or Plugin name
        public Dictionary<string, object> Properties { get; set; }
        public int Order { get; set; }
        public bool IsVisible { get; set; }
        public bool IsEnabled { get; set; }
        public string RequiredPermission { get; set; }

        public TabMetadata()
        {
            Id = Guid.NewGuid().ToString();
            Properties = new Dictionary<string, object>();
            IsVisible = true;
            IsEnabled = true;
        }
    }

    public enum TabType
    {
        Form,
        List,
        Dashboard,
        Report,
        Custom,
        Plugin
    }

    public class WidgetMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public WidgetType Type { get; set; }
        public string DataSource { get; set; }
        public Dictionary<string, object> Configuration { get; set; }
        public WidgetSize Size { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public int RowSpan { get; set; }
        public int ColumnSpan { get; set; }
        public bool IsVisible { get; set; }
        public string RefreshInterval { get; set; }

        public WidgetMetadata()
        {
            Id = Guid.NewGuid().ToString();
            Configuration = new Dictionary<string, object>();
            Size = WidgetSize.Medium;
            IsVisible = true;
            RowSpan = 1;
            ColumnSpan = 1;
        }
    }

    public enum WidgetType
    {
        Chart,
        Table,
        Card,
        KPI,
        Gauge,
        Map,
        Calendar,
        Custom
    }

    public enum WidgetSize
    {
        Small,
        Medium,
        Large,
        ExtraLarge,
        FullWidth
    }
}
// پایان کد