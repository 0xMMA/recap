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

    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(ZoomLevel), 1.0);

    public static readonly StyledProperty<double> ScrollOffsetProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(ScrollOffset), 0.0);

    public static readonly StyledProperty<double> PlaybackPositionProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(PlaybackPosition), -1.0);

    public static readonly StyledProperty<double> SelectionStartProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(SelectionStart), -1.0);

    public static readonly StyledProperty<double> SelectionEndProperty =
        AvaloniaProperty.Register<WaveformControl, double>(nameof(SelectionEnd), -1.0);

    public static readonly StyledProperty<float[]?> TrimPreviewProperty =
        AvaloniaProperty.Register<WaveformControl, float[]?>(nameof(TrimPreview));

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

    public double ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    public double ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    public double PlaybackPosition
    {
        get => GetValue(PlaybackPositionProperty);
        set => SetValue(PlaybackPositionProperty, value);
    }

    public double SelectionStart
    {
        get => GetValue(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    public double SelectionEnd
    {
        get => GetValue(SelectionEndProperty);
        set => SetValue(SelectionEndProperty, value);
    }

    public float[]? TrimPreview
    {
        get => GetValue(TrimPreviewProperty);
        set => SetValue(TrimPreviewProperty, value);
    }

    private enum DragTarget { None, Left, Right }
    private DragTarget _dragging = DragTarget.None;
    private bool _isSelecting;
    private bool _isPanning;
    private Point _panStart;
    private double _panStartOffset;

    static WaveformControl()
    {
        AffectsRender<WaveformControl>(PeaksProperty, IsRecordingProperty,
            IsTrimModeProperty, TrimLeftProperty, TrimRightProperty,
            ZoomLevelProperty, ScrollOffsetProperty, PlaybackPositionProperty,
            SelectionStartProperty, SelectionEndProperty, TrimPreviewProperty);

        IsTrimModeProperty.Changed.AddClassHandler<WaveformControl>((c, _) =>
        {
            if (!c.IsTrimMode)
            {
                c._dragging = DragTarget.None;
                c._isSelecting = false;
            }
        });
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var peaks = Peaks;
        if (peaks == null || peaks.Length == 0) return;

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (shift)
        {
            // Shift+wheel = horizontal pan
            if (ZoomLevel > 1.0)
            {
                ScrollOffset = Math.Clamp(ScrollOffset - e.Delta.Y * 0.05, 0.0, 1.0);
            }
        }
        else
        {
            // Regular wheel = zoom
            var pos = e.GetPosition(this);
            double oldZoom = ZoomLevel;
            double newZoom = e.Delta.Y > 0 ? oldZoom * 1.3 : oldZoom * 0.77;
            newZoom = Math.Clamp(newZoom, 1.0, 50.0);

            if (Math.Abs(newZoom - oldZoom) > 0.001)
            {
                // Center zoom on mouse position
                int totalPeaks = peaks.Length;
                int oldVisible = Math.Max(1, (int)(totalPeaks / oldZoom));
                int oldStart = (int)(ScrollOffset * Math.Max(0, totalPeaks - oldVisible));
                double mouseAudioIdx = oldStart + (pos.X / Bounds.Width) * oldVisible;

                ZoomLevel = newZoom;

                int newVisible = Math.Max(1, (int)(totalPeaks / newZoom));
                int maxStart = Math.Max(0, totalPeaks - newVisible);
                double newStart = mouseAudioIdx - (pos.X / Bounds.Width) * newVisible;
                ScrollOffset = maxStart > 0 ? Math.Clamp(newStart / maxStart, 0.0, 1.0) : 0.0;
            }
        }

        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var w = Bounds.Width;
        if (w < 1) return;

        var props = e.GetCurrentPoint(this).Properties;

        // Middle mouse drag = pan
        if (props.IsMiddleButtonPressed && ZoomLevel > 1.0)
        {
            _isPanning = true;
            _panStart = pos;
            _panStartOffset = ScrollOffset;
            e.Handled = true;
            return;
        }

        if (IsTrimMode)
        {
            var leftX = TrimLeft * w;
            var rightX = TrimRight * w;

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
        else if (props.IsLeftButtonPressed)
        {
            // Start selection
            _isSelecting = true;
            double fraction = PixelToAudioFraction(pos.X);
            SelectionStart = fraction;
            SelectionEnd = fraction;
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        var w = Bounds.Width;
        if (w < 1) return;

        if (_isPanning)
        {
            var peaks = Peaks;
            if (peaks != null && peaks.Length > 0)
            {
                int totalPeaks = peaks.Length;
                int visibleCount = Math.Max(1, (int)(totalPeaks / ZoomLevel));
                int maxStart = Math.Max(1, totalPeaks - visibleCount);
                double pixelDelta = pos.X - _panStart.X;
                double peakDelta = (pixelDelta / w) * visibleCount;
                ScrollOffset = Math.Clamp(_panStartOffset - peakDelta / maxStart, 0.0, 1.0);
            }
            e.Handled = true;
            return;
        }

        if (_dragging != DragTarget.None && IsTrimMode)
        {
            var fraction = Math.Clamp(pos.X / w, 0.0, 1.0);

            if (_dragging == DragTarget.Left)
                TrimLeft = Math.Min(fraction, TrimRight - 0.01);
            else if (_dragging == DragTarget.Right)
                TrimRight = Math.Max(fraction, TrimLeft + 0.01);

            e.Handled = true;
        }
        else if (_isSelecting && !IsTrimMode)
        {
            SelectionEnd = PixelToAudioFraction(pos.X);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
        {
            _isPanning = false;
            e.Handled = true;
            return;
        }

        if (_dragging != DragTarget.None)
        {
            _dragging = DragTarget.None;
            e.Handled = true;
        }

        if (_isSelecting)
        {
            _isSelecting = false;
            // Clear selection if too small
            if (SelectionStart >= 0 && SelectionEnd >= 0 &&
                Math.Abs(SelectionEnd - SelectionStart) < 0.001)
            {
                SelectionStart = -1;
                SelectionEnd = -1;
            }
            e.Handled = true;
        }
    }

    private double PixelToAudioFraction(double pixelX)
    {
        var peaks = Peaks;
        if (peaks == null || peaks.Length == 0) return 0;
        int totalPeaks = peaks.Length;
        int visibleCount = Math.Max(1, (int)(totalPeaks / ZoomLevel));
        int startIdx = (int)(ScrollOffset * Math.Max(0, totalPeaks - visibleCount));
        double fraction = pixelX / Bounds.Width;
        return Math.Clamp((startIdx + fraction * visibleCount) / totalPeaks, 0.0, 1.0);
    }

    private double AudioFractionToPixel(double audioFraction)
    {
        var peaks = Peaks;
        if (peaks == null || peaks.Length == 0) return 0;
        int totalPeaks = peaks.Length;
        int visibleCount = Math.Max(1, (int)(totalPeaks / ZoomLevel));
        int startIdx = (int)(ScrollOffset * Math.Max(0, totalPeaks - visibleCount));
        double posInView = (audioFraction * totalPeaks - startIdx) / visibleCount;
        return posInView * Bounds.Width;
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

        // Zoom/scroll calculations
        int totalPeaks = peaks.Length;
        int visibleCount = Math.Max(1, (int)(totalPeaks / ZoomLevel));
        int startIdx = (int)(ScrollOffset * Math.Max(0, totalPeaks - visibleCount));
        startIdx = Math.Clamp(startIdx, 0, Math.Max(0, totalPeaks - visibleCount));

        // Draw waveform as filled bars
        var barWidth = bounds.Width / visibleCount;
        var waveColor = IsRecording
            ? new SolidColorBrush(Color.FromRgb(220, 50, 50))   // Red when recording
            : new SolidColorBrush(Color.FromRgb(0, 180, 100));  // Green normally

        for (int i = 0; i < visibleCount && startIdx + i < totalPeaks; i++)
        {
            var amplitude = Math.Clamp(peaks[startIdx + i], 0f, 1f);
            if (amplitude < 0.005f) continue;

            var barHeight = amplitude * midY * 0.95;
            var x = i * barWidth;

            // Draw symmetric waveform (above and below center)
            var rect = new Rect(x, midY - barHeight, Math.Max(1, barWidth - 0.5), barHeight * 2);
            context.FillRectangle(waveColor, rect);
        }

        // Auto-trim preview overlay
        var trimPreview = TrimPreview;
        if (trimPreview != null && trimPreview.Length > 0 && !IsTrimMode)
        {
            var previewBrush = new SolidColorBrush(Color.FromArgb(100, 220, 50, 50));
            for (int i = 0; i < trimPreview.Length; i++)
            {
                if (trimPreview[i] > 0.5f)
                {
                    // Map preview index to audio fraction
                    double audioFrac = (double)i / trimPreview.Length;
                    double nextFrac = (double)(i + 1) / trimPreview.Length;
                    double px = AudioFractionToPixel(audioFrac);
                    double pxEnd = AudioFractionToPixel(nextFrac);
                    if (pxEnd > px && px < bounds.Width && pxEnd > 0)
                    {
                        px = Math.Max(0, px);
                        pxEnd = Math.Min(bounds.Width, pxEnd);
                        context.FillRectangle(previewBrush,
                            new Rect(px, 0, pxEnd - px, bounds.Height));
                    }
                }
            }
        }

        // Selection overlay (when not in trim mode)
        if (SelectionStart >= 0 && SelectionEnd >= 0 && !IsTrimMode)
        {
            double s = Math.Min(SelectionStart, SelectionEnd);
            double e = Math.Max(SelectionStart, SelectionEnd);
            double sx = AudioFractionToPixel(s);
            double ex = AudioFractionToPixel(e);
            if (ex > sx)
            {
                // Clamp to visible bounds
                sx = Math.Max(0, sx);
                ex = Math.Min(bounds.Width, ex);
                context.FillRectangle(
                    new SolidColorBrush(Color.FromArgb(60, 100, 150, 255)),
                    new Rect(sx, 0, ex - sx, bounds.Height));
            }
        }

        // Playback position indicator
        if (PlaybackPosition >= 0 && peaks.Length > 0)
        {
            double posInView = (PlaybackPosition * totalPeaks - startIdx) / visibleCount;
            if (posInView >= 0 && posInView <= 1)
            {
                double x = posInView * bounds.Width;
                var playPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 2);
                context.DrawLine(playPen, new Point(x, 0), new Point(x, bounds.Height));
            }
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
