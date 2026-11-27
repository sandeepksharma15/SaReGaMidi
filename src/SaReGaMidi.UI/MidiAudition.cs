using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Midi;

namespace SaReGaMidi.UI;

internal sealed class MidiAudition(Action<int> send)
{
    internal void SetAudible(int channel, bool audible)
    {
        // Channel volume also affects notes that were already sounding when the user clicked.
        send(0xB0 | (channel - 1) | (7 << 8) | ((audible ? 100 : 0) << 16));
    }

    internal async Task PlayAsync(byte[] midi, CancellationToken cancellationToken, Action<long>? melodyChanged = null)
    {
        MidiFile file = new(new MemoryStream(midi), true);
        var events = Enumerable.Range(0, file.Tracks).SelectMany(track => file.Events[track])
            .OrderBy(item => item.AbsoluteTime).GroupBy(item => item.AbsoluteTime);
        Stopwatch clock = Stopwatch.StartNew();
        long previousTick = 0;
        double milliseconds = 0;
        int microsPerQuarter = 500000;
        foreach (var group in events)
        {
            milliseconds += (group.Key - previousTick) * (double)microsPerQuarter
                / (1000 * file.DeltaTicksPerQuarterNote);
            previousTick = group.Key;
            double remaining = milliseconds - clock.Elapsed.TotalMilliseconds;
            if (remaining >= 1)
                await Task.Delay(TimeSpan.FromMilliseconds(remaining), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            bool melody = false;
            foreach (MidiEvent item in group)
            {
                if (item is TempoEvent tempo)
                    microsPerQuarter = tempo.MicrosecondsPerQuarterNote;
                else if (item is NoteEvent note)
                {
                    send(item.GetAsShortMessage());
                    melody |= note.Channel == 1;
                }
            }
            if (melody)
                melodyChanged?.Invoke(group.Key);
        }
    }
}
