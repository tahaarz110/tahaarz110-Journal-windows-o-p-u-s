// ابتدای فایل: Services/IThemeService.cs
// مسیر: /Services/IThemeService.cs

using System.Threading.Tasks;

namespace TradingJournal.Services
{
    public interface IThemeService
    {
        string CurrentTheme { get; }
        bool IsRTL { get; }
        
        void SetTheme(string themeName);
        void ToggleTheme();
        void SetRTL(bool isRtl);
        void ApplyTheme();
        Task SaveThemeSettingsAsync();
        Task LoadThemeSettingsAsync();
    }
}

// پایان فایل: Services/IThemeService.cs