using Recap.State;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Recap.Tui;

public class StatusBar : IRenderable
{
    private readonly SessionState _state;
    private readonly Func<bool> _hasApiKey;

    public StatusBar(SessionState state, Func<bool> hasApiKey)
    {
        _state = state;
        _hasApiKey = hasApiKey;
    }

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        return new Measurement(maxWidth, maxWidth);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var recState = _state.RecordingState switch
        {
            Models.RecordingState.Recording => "[red bold]● REC[/]",
            Models.RecordingState.Paused => "[yellow]⏸ PAUSED[/]",
            _ => "[grey]■ IDLE[/]"
        };

        var lang = $"[cyan]{Markup.Escape(_state.ActiveLanguage.ToUpper())}[/]";
        var segments = $"[blue]{_state.Segments.Count} seg[/]";
        var duration = $"[blue]{_state.TotalDuration:hh\\:mm\\:ss}[/]";
        var apiKey = _hasApiKey() ? "[green]API ✓[/]" : "[red]API ✗[/]";
        var mode = _state.Mode != Models.AppMode.Normal ? $"[yellow]{_state.Mode}[/]" : "";
        var dirty = _state.IsDirty ? "[yellow]*[/]" : "";

        var parts = new List<string> { recState, lang, segments, duration, apiKey };
        if (!string.IsNullOrEmpty(mode)) parts.Add(mode);
        if (!string.IsNullOrEmpty(dirty)) parts.Add(dirty);
        if (_state.LastActionResult != null)
            parts.Add($"[grey]{Markup.Escape(_state.LastActionResult)}[/]");

        var text = new Markup(string.Join(" │ ", parts));
        return ((IRenderable)text).Render(options, maxWidth);
    }
}
