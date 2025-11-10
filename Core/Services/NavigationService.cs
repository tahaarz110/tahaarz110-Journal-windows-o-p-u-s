// مسیر فایل: Core/Services/NavigationService.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TradingJournal.Core.MetadataEngine.Models;

namespace TradingJournal.Core.Services
{
    public interface INavigationService
    {
        void NavigateTo(string viewName);
        void NavigateToTab(TabMetadata tab);
        bool ShowDialog(string dialogName);
        void GoBack();
        bool CanGoBack { get; }
    }

    public class NavigationService : INavigationService
    {
        private readonly Stack<string> _navigationStack;
        private readonly Dictionary<string, Type> _viewMappings;
        private Frame _mainFrame;

        public NavigationService()
        {
            _navigationStack = new Stack<string>();
            _viewMappings = new Dictionary<string, Type>();
            RegisterViews();
        }

        public bool CanGoBack => _navigationStack.Count > 1;

        public void Initialize(Frame mainFrame)
        {
            _mainFrame = mainFrame;
        }

        public void NavigateTo(string viewName)
        {
            if (_viewMappings.TryGetValue(viewName, out Type viewType))
            {
                var view = Activator.CreateInstance(viewType) as Page;
                _mainFrame.Navigate(view);
                _navigationStack.Push(viewName);
            }
        }

        public void NavigateToTab(TabMetadata tab)
        {
            switch (tab.Type)
            {
                case TabType.Form:
                    NavigateToForm(tab.ContentSource);
                    break;
                case TabType.List:
                    NavigateToList(tab.ContentSource);
                    break;
                case TabType.Dashboard:
                    NavigateToDashboard(tab.ContentSource);
                    break;
                case TabType.Report:
                    NavigateToReport(tab.ContentSource);
                    break;
                case TabType.Plugin:
                    NavigateToPlugin(tab.ContentSource);
                    break;
            }
        }

        public bool ShowDialog(string dialogName)
        {
            if (_viewMappings.TryGetValue(dialogName, out Type dialogType))
            {
                var dialog = Activator.CreateInstance(dialogType) as Window;
                return dialog?.ShowDialog() ?? false;
            }
            return false;
        }

        public void GoBack()
        {
            if (CanGoBack)
            {
                _navigationStack.Pop();
                var previousView = _navigationStack.Peek();
                NavigateTo(previousView);
            }
        }

        private void NavigateToForm(string formName)
        {
            // Load dynamic form
            NavigateTo("DynamicFormView");
        }

        private void NavigateToList(string listName)
        {
            NavigateTo("TradeListView");
        }

        private void NavigateToDashboard(string dashboardName)
        {
            NavigateTo("DashboardView");
        }

        private void NavigateToReport(string reportName)
        {
            NavigateTo("ReportView");
        }

        private void NavigateToPlugin(string pluginName)
        {
            // Load plugin view
            NavigateTo("PluginHostView");
        }

        private void RegisterViews()
        {
            // Register all views and dialogs
            // This would be populated from assembly scanning or configuration
        }
    }

    public interface IMetadataService
    {
        Task<List<TabMetadata>> GetAllTabsAsync();
        Task<TabMetadata> GetTabAsync(string name);
        Task SaveTabAsync(TabMetadata tab);
    }

    public interface IThemeService
    {
        void ChangeTheme(string themeName);
        string CurrentTheme { get; }
        List<string> AvailableThemes { get; }
    }
}
// پایان کد