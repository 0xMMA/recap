using NAudio.Wave;
using Recap.Audio;
using Recap.State;
using Spectre.Console;
using Segment = Recap.Models.Segment;

namespace Recap.Tui;

public class TrimMode
{
    private readonly SessionState _state;
    private int _markerLeft;
    private int _markerRight;
    private int _totalColumns;
    private bool _leftActive = true;
    private float[] _peaks = Array.Empty<float>();

    public TrimMode(SessionState state)
    {
        _state = state;
    }

    public bool Run(Segment segment)
    {
        _totalColumns = Math.Max(20, AnsiConsole.Profile.Width - 6);
        _peaks = LoadPeaks(segment.FilePath, _totalColumns);
        _markerLeft = 0;
        _markerRight = _totalColumns - 1;

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold cyan]Trim Mode[/]");
        AnsiConsole.MarkupLine("[grey]←/→[/] Move marker │ [grey]Shift+←/→[/] Move 10 │ [grey]Tab[/] Switch marker │ [grey]Enter[/] Confirm │ [grey]Esc[/] Cancel\n");

        while (true)
        {
            RenderWaveform();

            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.LeftArrow:
                    MoveActiveMarker(key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? -10 : -1);
                    break;
                case ConsoleKey.RightArrow:
                    MoveActiveMarker(key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? 10 : 1);
                    break;
                case ConsoleKey.Tab:
                    _leftActive = !_leftActive;
                    break;
                case ConsoleKey.Enter:
                    ApplyTrim(segment);
                    return true;
                case ConsoleKey.Escape:
                    return false;
            }
        }
    }

    private void MoveActiveMarker(int delta)
    {
        if (_leftActive)
            _markerLeft = Math.Clamp(_markerLeft + delta, 0, _markerRight - 1);
        else
            _markerRight = Math.Clamp(_markerRight + delta, _markerLeft + 1, _totalColumns - 1);
    }

    private void RenderWaveform()
    {
        Console.SetCursorPosition(0, 3);
        var chars = new char[_totalColumns];
        var bars = new[] { ' ', '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

        for (int i = 0; i < _totalColumns && i < _peaks.Length; i++)
        {
            var level = Math.Clamp(_peaks[i], 0f, 1f);
            chars[i] = bars[(int)(level * (bars.Length - 1))];
        }

        for (int i = 0; i < _totalColumns; i++)
        {
            if (i == _markerLeft)
                AnsiConsole.Markup(_leftActive ? "[bold green]|[/]" : "[yellow]|[/]");
            else if (i == _markerRight)
                AnsiConsole.Markup(!_leftActive ? "[bold green]|[/]" : "[yellow]|[/]");
            else if (i > _markerLeft && i < _markerRight)
                AnsiConsole.Markup($"[cyan]{Markup.Escape(chars[i].ToString())}[/]");
            else
                AnsiConsole.Markup($"[grey]{Markup.Escape(chars[i].ToString())}[/]");
        }

        AnsiConsole.WriteLine();
        var activeLabel = _leftActive ? "[green]LEFT[/]" : "[green]RIGHT[/]";
        AnsiConsole.Markup($"  Active: {activeLabel}  L:{_markerLeft}  R:{_markerRight}  ");
        AnsiConsole.WriteLine();
    }

    private void ApplyTrim(Segment segment)
    {
        using var reader = new WaveFileReader(segment.FilePath);
        var totalSamples = reader.SampleCount;
        long startSample = (long)(_markerLeft / (float)_totalColumns * totalSamples);
        long endSample = (long)((_markerRight + 1) / (float)_totalColumns * totalSamples);

        var tempPath = segment.FilePath + ".trim.wav";
        using (var writer = new WaveFileWriter(tempPath, reader.WaveFormat))
        {
            reader.Position = startSample * reader.WaveFormat.BlockAlign;
            long bytesToRead = (endSample - startSample) * reader.WaveFormat.BlockAlign;
            var buffer = new byte[Math.Min(bytesToRead, 65536)];
            long remaining = bytesToRead;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = reader.Read(buffer, 0, toRead);
                if (read == 0) break;
                writer.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        File.Delete(segment.FilePath);
        File.Move(tempPath, segment.FilePath);

        using var trimmed = new WaveFileReader(segment.FilePath);
        segment.Duration = trimmed.TotalTime;
    }

    private static float[] LoadPeaks(string filePath, int columns)
    {
        try
        {
            using var reader = new WaveFileReader(filePath);
            var totalSamples = reader.SampleCount;
            var samplesPerCol = Math.Max(1, totalSamples / columns);
            var peaks = new float[columns];
            var buffer = new byte[2];

            for (int col = 0; col < columns; col++)
            {
                float peak = 0;
                reader.Position = (long)(col * samplesPerCol * reader.WaveFormat.BlockAlign);
                for (long s = 0; s < samplesPerCol; s++)
                {
                    if (reader.Read(buffer, 0, 2) != 2) break;
                    short sample = (short)(buffer[0] | (buffer[1] << 8));
                    peak = Math.Max(peak, Math.Abs(sample / 32768f));
                }
                peaks[col] = peak;
            }
            return peaks;
        }
        catch
        {
            return new float[columns];
        }
    }
}
