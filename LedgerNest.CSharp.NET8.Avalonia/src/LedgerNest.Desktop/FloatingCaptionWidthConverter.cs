using System.Globalization;
using Avalonia.Data.Converters;

namespace LedgerNest.Desktop;

/// <summary>Reserves the shared caption inset at both ends of an outlined field.</summary>
public sealed class FloatingCaptionWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double width && double.IsFinite(width) ? Math.Max(0, width - 24) : 0d;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
