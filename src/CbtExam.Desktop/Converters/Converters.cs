using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CbtExam.Desktop.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
        v is Visibility.Visible;
}

public class BoolToVisibilityInverseConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
        v is not Visibility.Visible;
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) => v is bool b && !b;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => v is bool b && !b;
}

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is true ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                  : new SolidColorBrush(Color.FromRgb(239, 68, 68));
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Shows Visible when string is non-empty, Collapsed when null/empty
public class NonEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        string.IsNullOrEmpty(v as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        string.IsNullOrWhiteSpace(v as string) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Shows Visible when int > 0, Collapsed when 0
public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Shows Collapsed when int > 0, Visible when 0  (inverse of above)
public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is int i && i == 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Converts a 0-100 percentage to a pixel width capped at maxWidth (default 100)
public class PercentageToWidthConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        double pct = v is double d ? d : v is int i ? i : 0;
        double max = p is string s && double.TryParse(s, out var parsed) ? parsed : 100.0;
        return Math.Max(0, Math.Min(max, pct / 100.0 * max));
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
public class PassThroughConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values?.ToArray() ?? Array.Empty<object>();
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
