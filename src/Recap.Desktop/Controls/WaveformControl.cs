using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Recap.Desktop.Controls;

public class WaveformControl : Control
{
    public static readonly StyledProperty<float[]?> PeaksProperty =
        AvaloniaProperty.Register<WaveformControl, float[]?>(nameof(Peaks));

    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(IsRecording));

    public float[]? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public bool IsRecording
    {
        get => GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    static WaveformControl()
    {
        AffectsRender<WaveformControl>(PeaksProperty, IsRecordingProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width < 1 || bounds.Height < 1) return;

        // Background
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 30)), bounds);

        // Center line
        var midY = bounds.Height / 2;
        var centerPen = new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), 1);
        context.DrawLine(centerPen, new Point(0, midY), new Point(bounds.Width, midY));

        var peaks = Peaks;
        if (peaks == null || peaks.Length == 0)
        {
            // "No data" text
            var text = new FormattedText("Select a segment",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Normal),
                14, new SolidColorBrush(Color.FromRgb(100, 100, 100)));
            context.DrawText(text, new Point(
                (bounds.Width - text.Width) / 2,
                (bounds.Height - text.Height) / 2));
            return;
        }

        // Draw waveform as filled bars
        var barWidth = Math.Max(1, bounds.Width / peaks.Length);
        var waveColor = IsRecording
            ? new SolidColorBrush(Color.FromRgb(220, 50, 50))   // Red when recording
            : new SolidColorBrush(Color.FromRgb(0, 180, 100));  // Green normally

        for (int i = 0; i < peaks.Length; i++)
        {
            var amplitude = Math.Clamp(peaks[i], 0f, 1f);
            if (amplitude < 0.005f) continue;

            var barHeight = amplitude * midY * 0.95;
            var x = i * barWidth;

            // Draw symmetric waveform (above and below center)
            var rect = new Rect(x, midY - barHeight, Math.Max(1, barWidth - 0.5), barHeight * 2);
            context.FillRectangle(waveColor, rect);
        }
    }
}
