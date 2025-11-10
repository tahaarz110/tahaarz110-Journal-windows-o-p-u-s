// ابتدای فایل: Services/ThemeService.cs
// مسیر: /Services/ThemeService.cs

using System;
using System.Threading.Tasks;
using System.Windows;
using MaterialDesignThemes.Wpf;
using TradingJournal.Core.Configuration;
using Serilog;

namespace TradingJournal.Services
{
    public class ThemeService : IThemeService
    {
        private readonly PaletteHelper _paletteHelper;
        private string _currentTheme;
        private bool _isRtl;

        public ThemeService()
        {
            _paletteHelper = new PaletteHelper();
            _currentTheme = AppSettings.Instance.Theme;
            _isRtl = AppSettings.Instance.IsRTL;
        }

        public string CurrentTheme => _currentTheme;
        public bool IsRTL => _isRtl;

        public void SetTheme(string themeName)
        {
            _currentTheme = themeName;
            ApplyTheme();
        }

        public void ToggleTheme()
        {
            _currentTheme = _currentTheme == "Dark" ? "Light" : "Dark";
            ApplyTheme();
            _ = SaveThemeSettingsAsync();
        }

        public void SetRTL(bool isRtl)
        {
            _isRtl = isRtl;
            ApplyRTL();
            _ = SaveThemeSettingsAsync();
        }

        public void ApplyTheme()
        {
            try
            {
                var theme = _paletteHelper.GetTheme();
                
                // Set base theme
                theme.SetBaseTheme(_currentTheme == "Dark" ? Theme.Dark : Theme.Light);
                
                // Set primary color
                theme.SetPrimaryColor(System.Windows.Media.Color.FromRgb(33, 150, 243)); // Blue
                
                // Set secondary color  
                theme.SetSecondaryColor(System.Windows.Media.Color.FromRgb(255, 193, 7)); // Amber
                
                _paletteHelper.SetTheme(theme);
                
                Log.Information($"Theme changed to: {_currentTheme}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error applying theme");
            }
        }

        private void ApplyRTL()
        {
            try
            {
                var app = Application.Current;
                if (app != null)
                {
                    app.Resources["FlowDirection"] = _isRtl ? 
                        FlowDirection.RightToLeft : 
                        FlowDirection.LeftToRight;
                }
                
                Log.Information($"RTL changed to: {_isRtl}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error applying RTL");
            }
        }

        public async Task SaveThemeSettingsAsync()
        {
            try
            {
                AppSettings.Instance.Theme = _currentTheme;
                AppSettings.Instance.IsRTL = _isRtl;
                AppSettings.Save();
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving theme settings");
            }
        }

        public async Task LoadThemeSettingsAsync()
        {
            try
            {
                _currentTheme = AppSettings.Instance.Theme;
                _isRtl = AppSettings.Instance.IsRTL;
                
                ApplyTheme();
                ApplyRTL();
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading theme settings");
            }
        }
    }
}

// پایان فایل: Services/ThemeService.cs