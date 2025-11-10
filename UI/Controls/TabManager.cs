// ابتدای فایل: UI/Controls/TabManager.cs
// مسیر: /UI/Controls/TabManager.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using Serilog;
using TradingJournal.Core.MetadataEngine;
using TradingJournal.Data.Models;

namespace TradingJournal.UI.Controls
{
    public class TabManager
    {
        private readonly TabControl _tabControl;
        private readonly MetadataEngine _metadataEngine;
        private readonly Dictionary<string, TabItem> _tabs;

        public TabManager(TabControl tabControl)
        {
            _tabControl = tabControl;
            _metadataEngine = new MetadataEngine();
            _tabs = new Dictionary<string, TabItem>();
        }

        public async void LoadTabsFromMetadata()
        {
            try
            {
                var tabKeys = await _metadataEngine.GetMetadataKeysAsync("tabs");
                
                foreach (var tabKey in tabKeys)
                {
                    var metadata = await _metadataEngine.GetMetadataAsync(tabKey);
                    if (metadata != null)
                    {
                        AddTab(metadata);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading tabs from metadata");
            }
        }

        public TabItem AddTab(JObject tabMetadata)
        {
            var tabId = tabMetadata["tabId"]?.ToString() ?? Guid.NewGuid().ToString();
            var tabName = tabMetadata["tabNameFa"]?.ToString() ?? tabMetadata["tabName"]?.ToString() ?? "New Tab";
            var tabType = tabMetadata["tabType"]?.ToString() ?? "custom";
            var iconName = tabMetadata["iconName"]?.ToString();
            var isCloseable = tabMetadata["isCloseable"]?.Value<bool>() ?? true;

            // Check if tab already exists
            if (_tabs.ContainsKey(tabId))
            {
                // Select existing tab
                _tabControl.SelectedItem = _tabs[tabId];
                return _tabs[tabId];
            }

            // Create tab header
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Add icon if specified
            if (!string.IsNullOrEmpty(iconName) && Enum.TryParse<PackIconKind>(iconName, out var iconKind))
            {
                headerStack.Children.Add(new PackIcon
                {
                    Kind = iconKind,
                    Width = 18,
                    Height = 18,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            // Add title
            headerStack.Children.Add(new TextBlock
            {
                Text = tabName,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Add close button if closeable
            if (isCloseable)
            {
                var closeButton = new Button
                {
                    Content = new PackIcon
                    {
                        Kind = PackIconKind.Close,
                        Width = 14,
                        Height = 14
                    },
                    Style = Application.Current.FindResource("MaterialDesignFlatButton") as Style,
                    Padding = new Thickness(4),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                closeButton.Click += (s, e) =>
                {
                    e.Handled = true;
                    RemoveTab(tabId);
                };

                headerStack.Children.Add(closeButton);
            }

            // Create tab item
            var tabItem = new TabItem
            {
                Header = headerStack,
                Name = $"Tab_{tabId.Replace("-", "_")}",
                Tag = tabMetadata
            };

            // Create content based on tab type
            tabItem.Content = CreateTabContent(tabType, tabMetadata);

            // Add to tab control and dictionary
            _tabControl.Items.Add(tabItem);
            _tabs[tabId] = tabItem;

            // Select the new tab
            _tabControl.SelectedItem = tabItem;

            return tabItem;
        }

        private UIElement CreateTabContent(string tabType, JObject metadata)
        {
            switch (tabType.ToLower())
            {
                case "tradelist":
                    return CreateTradeListContent(metadata);
                
                case "dashboard":
                    return CreateDashboardContent(metadata);
                
                case "form":
                    return CreateFormContent(metadata);
                
                case "report":
                    return CreateReportContent(metadata);
                
                case "custom":
                default:
                    return CreateCustomContent(metadata);
            }
        }

        private UIElement CreateTradeListContent(JObject metadata)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Toolbar
            var toolbar = new ToolBar();
            toolbar.Items.Add(new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new PackIcon { Kind = PackIconKind.Plus, Margin = new Thickness(0, 0, 5, 0) },
                        new TextBlock { Text = "جدید" }
                    }
                }
            });
            Grid.SetRow(toolbar, 0);
            grid.Children.Add(toolbar);

            // DataGrid
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false
            };
            Grid.SetRow(dataGrid, 1);
            grid.Children.Add(dataGrid);

            return grid;
        }

        private UIElement CreateDashboardContent(JObject metadata)
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var wrapPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10)
            };

            // Add placeholder widgets
            for (int i = 0; i < 4; i++)
            {
                var card = new Card
                {
                    Width = 250,
                    Height = 120,
                    Margin = new Thickness(10),
                    Content = new TextBlock
                    {
                        Text = $"Widget {i + 1}",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                wrapPanel.Children.Add(card);
            }

            scrollViewer.Content = wrapPanel;
            return scrollViewer;
        }

        private UIElement CreateFormContent(JObject metadata)
        {
            var formBuilder = new FormBuilder();
            return formBuilder.BuildForm(metadata);
        }

        private UIElement CreateReportContent(JObject metadata)
        {
            return new TextBlock
            {
                Text = "Report content will be loaded here",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 18
            };
        }

        private UIElement CreateCustomContent(JObject metadata)
        {
            return new TextBlock
            {
                Text = "Custom content",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public void RemoveTab(string tabId)
        {
            if (_tabs.TryGetValue(tabId, out var tab))
            {
                _tabControl.Items.Remove(tab);
                _tabs.Remove(tabId);
            }
        }

        public void SelectTab(string tabId)
        {
            if (_tabs.TryGetValue(tabId, out var tab))
            {
                _tabControl.SelectedItem = tab;
            }
        }

        public TabItem? GetTab(string tabId)
        {
            return _tabs.TryGetValue(tabId, out var tab) ? tab : null;
        }

        public void ClearAllTabs()
        {
            _tabControl.Items.Clear();
            _tabs.Clear();
        }
    }
}

// پایان فایل: UI/Controls/TabManager.cs