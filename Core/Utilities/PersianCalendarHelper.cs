// مسیر فایل: Core/Utilities/PersianCalendarHelper.cs
// ابتدای کد
using System;
using System.Globalization;

namespace TradingJournal.Core.Utilities
{
    public static class PersianCalendarHelper
    {
        private static readonly PersianCalendar _persianCalendar = new PersianCalendar();
        private static readonly string[] _monthNames = 
        {
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        };

        private static readonly string[] _dayNames = 
        {
            "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", 
            "پنج‌شنبه", "جمعه", "شنبه"
        };

        public static string ToPersianDate(DateTime date, string format = "yyyy/MM/dd")
        {
            var year = _persianCalendar.GetYear(date);
            var month = _persianCalendar.GetMonth(date);
            var day = _persianCalendar.GetDayOfMonth(date);

            return format
                .Replace("yyyy", year.ToString("D4"))
                .Replace("yy", (year % 100).ToString("D2"))
                .Replace("MMMM", GetMonthName(month))
                .Replace("MM", month.ToString("D2"))
                .Replace("M", month.ToString())
                .Replace("dd", day.ToString("D2"))
                .Replace("d", day.ToString());
        }

        public static string ToPersianDateTime(DateTime dateTime)
        {
            var date = ToPersianDate(dateTime);
            var time = dateTime.ToString("HH:mm:ss");
            return $"{date} {time}";
        }

        public static DateTime FromPersianDate(int year, int month, int day)
        {
            return _persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        }

        public static DateTime FromPersianDate(string persianDate)
        {
            var parts = persianDate.Split('/', '-');
            if (parts.Length != 3)
                throw new ArgumentException("Invalid Persian date format");

            var year = int.Parse(parts[0]);
            var month = int.Parse(parts[1]);
            var day = int.Parse(parts[2]);

            return FromPersianDate(year, month, day);
        }

        public static string GetMonthName(int month)
        {
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(month));
            
            return _monthNames[month - 1];
        }

        public static string GetDayName(DayOfWeek dayOfWeek)
        {
            return _dayNames[(int)dayOfWeek];
        }

        public static int GetPersianYear(DateTime date)
        {
            return _persianCalendar.GetYear(date);
        }

        public static int GetPersianMonth(DateTime date)
        {
            return _persianCalendar.GetMonth(date);
        }

        public static int GetPersianDay(DateTime date)
        {
            return _persianCalendar.GetDayOfMonth(date);
        }

        public static DayOfWeek GetPersianDayOfWeek(DateTime date)
        {
            return _persianCalendar.GetDayOfWeek(date);
        }

        public static string GetPersianMonthYear(DateTime date)
        {
            var year = GetPersianYear(date);
            var month = GetPersianMonth(date);
            return $"{GetMonthName(month)} {year}";
        }

        public static DateTime GetPersianMonthStart(DateTime date)
        {
            var year = GetPersianYear(date);
            var month = GetPersianMonth(date);
            return FromPersianDate(year, month, 1);
        }

        public static DateTime GetPersianMonthEnd(DateTime date)
        {
            var year = GetPersianYear(date);
            var month = GetPersianMonth(date);
            var daysInMonth = _persianCalendar.GetDaysInMonth(year, month);
            return FromPersianDate(year, month, daysInMonth);
        }

        public static int GetPersianWeekOfYear(DateTime date)
        {
            return _persianCalendar.GetWeekOfYear(
                date, 
                CalendarWeekRule.FirstDay, 
                DayOfWeek.Saturday);
        }
    }
}
// پایان کد