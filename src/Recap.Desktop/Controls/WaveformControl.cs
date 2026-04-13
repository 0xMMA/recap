using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Recap.Desktop.Controls;

public class WaveformControl : Control
{
    public static readonly StyledProperty<float[]?> PeaksProperty =
        AvaloniaProperty.Register<WaveformControl, float[]?>(nameof(Peaks));

    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(IsRecording));

    public static readonly StyledProperty<bool> IsTrimModeProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(IsTrimMode));

    public static readonly StyledProperty<double> TrimLeftProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(TrimLeft), 0.0);

    public static readonly StyledProperty<double> TrimRightProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(TrimRight), 1.0);

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

    public bool IsTrimMode
    {
        get => GetValue(IsTrimModeProperty);
        set => SetValue(IsTrimModeProperty, value);
    }

    public double TrimLeft
    {
        get => GetValue(TrimLeftProperty);
        set => SetValue(TrimLeftProperty, value);
    }

    public double TrimRight
    {
        get => GetValue(TrimRightProperty);
        set => SetValue(TrimRightProperty, value);
    }

    private enum DragTarget { None, Left, Right }
    private DragTarget _dragging = DragTarget.None;

    static WaveformControl()
    {
        AffectsRender<WaveformControl>(PeaksProperty, IsRecordingProperty,
            IsTrimModeProperty, TrimLeftProperty, TrimRightProperty);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsTrimMode) return;

        var pos = e.GetPosition(this);
        var w = Bounds.Width;
        if (w < 1) return;

        var leftX = TrimLeft * w;
        var rightX = TrimRight * w;

        // Pick the closer marker if within 10px
        var distLeft = Math.Abs(pos.X - leftX);
        var distRight = Math.Abs(pos.X - rightX);

        if (distLeft <= 10 && distLeft <= distRight)
            _dragging = DragTarget.Left;
        else if (distRight <= 10)
            _dragging = DragTarget.Right;
        else
            _dragging = DragTarget.None;

        if (_dragging != DragTarget.None)
            e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging == DragTarget.None || !IsTrimMode) return;

        var pos = e.GetPosition(this);
        var w = Bounds.Width;
        if (w < 1) return;

        var fraction = Math.Clamp(pos.X / w, 0.0, 1.0);

        if (_dragging == DragTarget.Left)
        {
            TrimLeft = Math.Min(fraction, TrimRight - 0.01);
        }
        else if (_dragging == DragTarget.Right)
        {
            TrimRight = Math.Max(fraction, TrimLeft + 0.01);
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging != DragTarget.None)
        {
            _dragging = DragTarget.None;
            e.Handled = true;
        }
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

        // Trim mode overlays
        if (IsTrimMode)
        {
            var overlayBrush = new SolidColorBrush(Color.FromArgb(120, 40, 40, 40));
            var leftX = TrimLeft * bounds.Width;
            var rightX = TrimRight * bounds.Width;

            // Gray overlay on excluded regions
            if (leftX > 0)
                context.FillRectangle(overlayBrush, new Rect(0, 0, leftX, bounds.Height));
            if (rightX < bounds.Width)
                context.FillRectangle(overlayBrush, new Rect(rightX, 0, bounds.Width - rightX, bounds.Height));

            // Left marker (yellow)
            var leftPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 220, 50)), 2);
            context.DrawLine(leftPen, new Point(leftX, 0), new Point(leftX, bounds.Height));

            // Right marker (cyan)
            var rightPen = new Pen(new SolidColorBrush(Color.FromRgb(50, 220, 255)), 2);
            context.DrawLine(rightPen, new Point(rightX, 0), new Point(rightX, bounds.Height));

            // Hint text
            var hint = new FormattedText("Trim: drag markers, Enter confirm, Esc cancel",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Normal),
                11, new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)));
            context.DrawText(hint, new Point(
                (bounds.Width - hint.Width) / 2, 4));
        }
    }
}
