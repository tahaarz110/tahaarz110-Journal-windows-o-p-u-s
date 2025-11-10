// مسیر فایل: UI/Converters/WidgetConverters.cs
// ابتدای کد
using System;
using System.Globalization;
using System.Windows.Data;
using TradingJournal.Core.PluginEngine;

namespace TradingJournal.UI.Converters
{
    public class WidgetTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string widgetType)
            {
                return widgetType switch
                {
                    "Chart" => "ChartLine",
                    "Table" => "Table",
                    "Card" => "CardText",
                    "KPI" => "Gauge",
                    "Gauge" => "Speedometer",
                    _ => "WidgetsOutline"
                };
            }
            return "WidgetsOutline";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToEnableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PluginManager.PluginStatus status)
            {
                return status == PluginManager.PluginStatus.Stopped;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToDisableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PluginManager.PluginStatus status)
            {
                return status == PluginManager.PluginStatus.Running;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToFalseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
// پایان کد