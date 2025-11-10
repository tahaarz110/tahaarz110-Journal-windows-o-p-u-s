// مسیر فایل: Core/Services/ThemeService.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using MaterialDesignColors;

namespace TradingJournal.Core.Services
{
    public class ThemeService : IThemeService
    {
        private readonly PaletteHelper _paletteHelper;
        private string _currentTheme = "Light";

        public string CurrentTheme => _currentTheme;

        public List<string> AvailableThemes => new List<string> 
        { 
            "Light", 
            "Dark", 
            "Auto" 
        };

        public List<string> AvailableColors => new List<string>
        {
            "Red", "Pink", "Purple", "DeepPurple", "Indigo", 
            "Blue", "LightBlue", "Cyan", "Teal", "Green",
            "LightGreen", "Lime", "Yellow", "Amber", "Orange",
            "DeepOrange", "Brown", "Grey", "BlueGrey"
        };

        public ThemeService()
        {
            _paletteHelper = new PaletteHelper();
        }

        public void ChangeTheme(string themeName)
        {
            var theme = _paletteHelper.GetTheme();

            switch (themeName?.ToLower())
            {
                case "dark":
                    theme.SetBaseTheme(Theme.Dark);
                    _currentTheme = "Dark";
                    break;
                case "light":
                    theme.SetBaseTheme(Theme.Light);
                    _currentTheme = "Light";
                    break;
                case "auto":
                    var isDarkMode = IsSystemDarkMode();
                    theme.SetBaseTheme(isDarkMode ? Theme.Dark : Theme.Light);
                    _currentTheme = "Auto";
                    break;
            }

            _paletteHelper.SetTheme(theme);
            SaveThemeSettings();
        }

        public void ChangePrimaryColor(string colorName)
        {
            var theme = _paletteHelper.GetTheme();
            var color = GetColorFromName(colorName);
            
            if (color != null)
            {
                theme.SetPrimaryColor(color.Value);
                _paletteHelper.SetTheme(theme);
                SaveThemeSettings();
            }
        }

        public void ChangeAccentColor(string colorName)
        {
            var theme = _paletteHelper.GetTheme();
            var color = GetColorFromName(colorName);
            
            if (color != null)
            {
                theme.SetSecondaryColor(color.Value);
                _paletteHelper.SetTheme(theme);
                SaveThemeSettings();
            }
        }

        public void ApplyRtlTheme()
        {
            Application.Current.Resources["DefaultFlowDirection"] = FlowDirection.RightToLeft;
            
            // Apply RTL specific styles
            var rtlDict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/UI/Styles/RtlStyles.xaml")
            };
            
            Application.Current.Resources.MergedDictionaries.Add(rtlDict);
        }

        public void ApplyFontSettings(string fontFamily, double fontSize)
        {
            Application.Current.Resources["DefaultFontFamily"] = new FontFamily(fontFamily);
            Application.Current.Resources["DefaultFontSize"] = fontSize;
        }

        private Color? GetColorFromName(string colorName)
        {
            var swatchesProvider = new SwatchesProvider();
            var swatches = swatchesProvider.Swatches;
            
            foreach (var swatch in swatches)
            {
                if (swatch.Name.Equals(colorName, StringComparison.OrdinalIgnoreCase))
                {
                    return swatch.ExemplarHue.Color;
                }
            }
            
            return null;
        }

        private bool IsSystemDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                
                var value = key?.GetValue("AppsUseLightTheme");
                return value is int intValue && intValue == 0;
            }
            catch
            {
                return false;
            }
        }

        private void SaveThemeSettings()
        {
            // Save to configuration
            Properties.Settings.Default.Theme = _currentTheme;
            Properties.Settings.Default.Save();
        }
    }
}
// پایان کد