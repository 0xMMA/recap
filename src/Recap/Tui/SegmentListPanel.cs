using Recap.State;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Recap.Tui;

public class SegmentListPanel : IRenderable
{
    private readonly SessionState _state;
    private const int SparklineWidth = 20;

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
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Idx").Width(4))
            .AddColumn(new TableColumn("Dur").Width(10))
            .AddColumn(new TableColumn("Wave").Width(SparklineWidth));

        if (_state.Segments.Count == 0)
        {
            var empty = new Markup("[grey]No segments. Press [bold]R[/] to record.[/]");
            return ((IRenderable)empty).Render(options, maxWidth);
        }

        var (selStart, selEnd) = _state.GetSelectionRange();

        foreach (var seg in _state.Segments)
        {
            bool selected = seg.Index >= selStart && seg.Index <= selEnd;
            var prefix = selected ? "[bold green]>[/] " : "  ";
            var style = selected ? "bold" : "dim";

            if (!seg.HasFile)
            {
                var dots = new string('·', SparklineWidth);
                table.AddRow(
                    new Markup($"{prefix}[{style}]{seg.Index + 1:D2}[/]"),
                    new Markup($"[red]⚠ {seg.Duration:mm\\:ss\\.f}[/]"),
                    new Markup($"[red]{dots}[/]")
                );
            }
            else
            {
                var sparkline = WaveformRenderer.GetOrRenderSegment(seg.FilePath, SparklineWidth);
                table.AddRow(
                    new Markup($"{prefix}[{style}]{seg.Index + 1:D2}[/]"),
                    new Markup($"[{style}]{seg.Duration:mm\\:ss\\.f}[/]"),
                    new Markup($"[cyan]{Markup.Escape(sparkline)}[/]")
                );
            }
        }

        return ((IRenderable)table).Render(options, maxWidth);
    }
}
