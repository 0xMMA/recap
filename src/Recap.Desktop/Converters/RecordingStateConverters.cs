using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Recap.Core.Models;

namespace Recap.Desktop.Converters;

public static class RecordingStateConverters
{
    public static readonly IValueConverter Icon = new RecordingIconConverter();
    public static readonly IValueConverter Foreground = new RecordingForegroundConverter();
    public static readonly IValueConverter StatusLabel = new RecordingStatusLabelConverter();
    public static readonly IValueConverter ApiStatus = new ApiStatusConverter();
    public static readonly IValueConverter ApiColor = new ApiColorConverter();
    public static readonly IValueConverter IsRecording = new IsRecordingConverter();

    private class RecordingIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is RecordingState state && state == RecordingState.Recording)
                return "\u25A0"; // ■ stop
            return "\u25CF"; // ● record
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class RecordingForegroundConverter : IValueConverter
    {
        private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#E44"));
        private static readonly IBrush Normal = new SolidColorBrush(Color.Parse("#CCC"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is RecordingState state && state == RecordingState.Recording)
                return Red;
            return Normal;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class RecordingStatusLabelConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is RecordingState state
                ? state switch
                {
                    RecordingState.Recording => "\u25CF REC",
                    RecordingState.Paused => "\u23F8 PAUSED",
                    _ => "\u25A0 IDLE"
                }
                : "\u25A0 IDLE";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class ApiStatusConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? "\u2713" : "\u2717";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class ApiColorConverter : IValueConverter
    {
        private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#6C6"));
        private static readonly IBrush Gray = new SolidColorBrush(Color.Parse("#888"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? Green : Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class IsRecordingConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is RecordingState state && state == RecordingState.Recording;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
