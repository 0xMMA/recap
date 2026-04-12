using Recap.State;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Recap.Tui;

public class SegmentListPanel : IRenderable
{
    private readonly SessionState _state;
    private const int IdxWidth = 4;
    private const int DurWidth = 10;
    private const int Padding = 3; // column gaps

    public SegmentListPanel(SessionState state)
    {
        _state = state;
    }

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        return new Measurement(20, maxWidth);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        if (_state.Segments.Count == 0)
        {
            var empty = new Markup("[grey]No segments. Press [bold]R[/] to record.[/]");
            return ((IRenderable)empty).Render(options, maxWidth);
        }

        // Dynamic sparkline width — use all available space
        int sparkWidth = Math.Max(10, maxWidth - IdxWidth - DurWidth - Padding);

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Idx").Width(IdxWidth))
            .AddColumn(new TableColumn("Dur").Width(DurWidth))
            .AddColumn(new TableColumn("Wave"));

        var (selStart, selEnd) = _state.GetSelectionRange();

        foreach (var seg in _state.Segments)
        {
            bool selected = seg.Index >= selStart && seg.Index <= selEnd;
            var prefix = selected ? "[bold green]>[/] " : "  ";
            var style = selected ? "bold" : "dim";

            if (!seg.HasFile)
            {
                var dots = new string('·', sparkWidth);
                table.AddRow(
                    new Markup($"{prefix}[{style}]{seg.Index + 1:D2}[/]"),
                    new Markup($"[red]⚠ {seg.Duration:mm\\:ss\\.f}[/]"),
                    new Markup($"[red]{dots}[/]")
                );
            }
            else
            {
                var peaks = WaveformRenderer.GetOrLoadPeaks(seg.FilePath, sparkWidth);
                var (top, bottom) = WaveformRenderer.RenderHalfBlock(peaks, sparkWidth);

                // Two-row rendering: top row has index+duration, bottom row is continuation
                table.AddRow(
                    new Markup($"{prefix}[{style}]{seg.Index + 1:D2}[/]"),
                    new Markup($"[{style}]{seg.Duration:mm\\:ss\\.f}[/]"),
                    new Markup(top)
                );
                table.AddRow(
                    new Markup(""),
                    new Markup(""),
                    new Markup(bottom)
                );
            }
        }

        return ((IRenderable)table).Render(options, maxWidth);
    }
}
