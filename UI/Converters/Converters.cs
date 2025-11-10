// مسیر فایل: UI/Converters/Converters.cs
// ابتدای کد
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TradingJournal.UI.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }
    }

    public class ProfitToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal profit)
            {
                if (profit > 0)
                    return new SolidColorBrush(Colors.Green);
                else if (profit < 0)
                    return new SolidColorBrush(Colors.Red);
                else
                    return new SolidColorBrush(Colors.Gray);
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DecimalToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                string format = parameter as string ?? "N2";
                return decimalValue.ToString(format, CultureInfo.CurrentCulture);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && decimal.TryParse(stringValue, out decimal result))
            {
                return result;
            }
            return 0m;
        }
    }

    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                string format = parameter as string ?? "yyyy/MM/dd HH:mm";
                var persianCalendar = new PersianCalendar();
                
                if (format.Contains("persian"))
                {
                    return $"{persianCalendar.GetYear(dateTime)}/{persianCalendar.GetMonth(dateTime):D2}/{persianCalendar.GetDayOfMonth(dateTime):D2}";
                }
                
                return dateTime.ToString(format, CultureInfo.CurrentCulture);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && DateTime.TryParse(stringValue, out DateTime result))
            {
                return result;
            }
            return DateTime.Now;
        }
    }

    public class EnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum enumValue)
            {
                var field = enumValue.GetType().GetField(enumValue.ToString());
                var attributes = field?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false) as System.ComponentModel.DataAnnotations.DisplayAttribute[];
                
                if (attributes?.Length > 0)
                {
                    return attributes[0].Name;
                }
                
                return enumValue.ToString();
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MultiplyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue && parameter is string factor && double.TryParse(factor, out double multiplier))
            {
                return doubleValue * multiplier;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue && parameter is string factor && double.TryParse(factor, out double multiplier))
            {
                return doubleValue / multiplier;
            }
            return value;
        }
    }

    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                return $"{percentage:F2}%";
            }
            return "0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                str = str.Replace("%", "");
                if (double.TryParse(str, out double result))
                {
                    return result;
                }
            }
            return 0.0;
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inverse = parameter?.ToString() == "inverse";
            bool isNull = value == null;
            
            if (inverse)
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            else
                return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
// پایان کد