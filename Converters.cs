using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AirlineEmpire.Models;

namespace AirlineEmpire.Converters
{
    // LogLevel 定义在这里，MainViewModel 通过 using AirlineEmpire.Converters 引用
    public enum LogLevel { Info, Alert, Error }
    public record LogEntry(string Time, string Message, LogLevel Level);

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            bool b = v is bool bv && bv;
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => v is Visibility vis && vis == Visibility.Visible;
    }

    [ValueConversion(typeof(UrgencyLevel), typeof(Brush))]
    public class UrgencyToColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            return (UrgencyLevel)v switch
            {
                UrgencyLevel.Critical => new SolidColorBrush(Color.FromRgb(255, 61, 61)),
                UrgencyLevel.High => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                UrgencyLevel.Medium => new SolidColorBrush(Color.FromRgb(0, 230, 118)),
                _ => new SolidColorBrush(Color.FromRgb(43, 127, 255)),
            };
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => DependencyProperty.UnsetValue;
    }

    [ValueConversion(typeof(WearLevel), typeof(Brush))]
    public class WearToColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            return (WearLevel)v switch
            {
                WearLevel.Scrapped => new SolidColorBrush(Color.FromRgb(150, 50, 50)),
                WearLevel.Critical => new SolidColorBrush(Color.FromRgb(255, 61, 61)),
                WearLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                _ => new SolidColorBrush(Color.FromRgb(0, 230, 118)),
            };
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => DependencyProperty.UnsetValue;
    }

    public class RatioToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type t, object p, CultureInfo c)
        {
            if (values.Length < 2) return 0d;
            if (values[0] is not double ratio) return 0d;
            double maxW = p is string ps && double.TryParse(ps, out double pw)
                ? pw : values[1] is double dw ? dw : 100d;
            return Math.Max(0, Math.Min(maxW, ratio * maxW));
        }
        public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c)
            => throw new NotImplementedException();
    }

    [ValueConversion(typeof(double), typeof(double))]
    public class RatioToFixedWidthConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            double ratio = v is double d ? d : 0;
            double maxW = p is string ps && double.TryParse(ps, out double pw) ? pw : 100;
            return Math.Max(0, Math.Min(maxW, ratio * maxW));
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => DependencyProperty.UnsetValue;
    }

    [ValueConversion(typeof(bool), typeof(string))]
    public class BoolToUpgradeTextConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => v is bool b && b ? "升级机场" : "已满级";
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => DependencyProperty.UnsetValue;
    }

    [ValueConversion(typeof(decimal), typeof(string))]
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => v is decimal d ? $"¥ {d:N0}" : "¥ 0";
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => DependencyProperty.UnsetValue;
    }

    [ValueConversion(typeof(LogLevel), typeof(Brush))]
    public class LogLevelToColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            return (LogLevel)v switch
            {
                LogLevel.Alert => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                LogLevel.Error => new SolidColorBrush(Color.FromRgb(255, 61, 61)),
                _ => new SolidColorBrush(Color.FromRgb(96, 120, 152)),
            };
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => DependencyProperty.UnsetValue;
    }
}