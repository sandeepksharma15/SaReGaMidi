using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Midi;
using SaReGaMidi.Music;
using SaReGaMidi.Music.Rhythm;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

internal static class MidiScoreExport
{
    private const int Resolution = 960;

    internal static byte[] Render(CompositionScore score, ScorePass passage, int sa, int tempo,
        IDictionary<long, (int Maatra, int Swara)>? noteStarts = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tempo, 20);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tempo, 400);
        Taal taal = TaalCatalog.FindByName(score.TaalName) ?? throw new InvalidDataException("Choose a taal.");
        string?[] bols = taal.Vibhaags.SelectMany(division => division.Theka).ToArray();
        MidiEventCollection file = new(1, Resolution);
        IList<MidiEvent> melody = file.AddTrack();
        IList<MidiEvent> drums = file.AddTrack();
        melody.Add(new TextEvent("Melody", MetaEventType.SequenceTrackName, 0));
        melody.Add(new TempoEvent((int)Math.Round(60_000_000d / tempo), 0));
        melody.Add(new TimeSignatureEvent(0, taal.MaatraCount, 2, 24, 8));
        drums.Add(new TextEvent($"Theka - {taal.Name}", MetaEventType.SequenceTrackName, 0));

        // A section may restart on a different taal beat. Align that entry to the next matching grid beat.
        long elapsed = 0;
        int rangeEnd = checked(passage.StartMaatra + passage.MaatraCount);
        if (passage.StartMaatra < 0 || passage.MaatraCount < 1 || rangeEnd > score.Sections.Sum(s => s.Maatras.Count))
            throw new InvalidDataException("Choose a non-empty passage within the score.");

        PlaybackTimeline timeline = ScorePerformance.BuildTimeline(score, [passage]);
        if (timeline.UnclearBeats.FirstOrDefault() is PlaybackBeat unclear)
            throw new InvalidDataException($"Resolve the unclear swara in {unclear.Section}, beat {unclear.TaalBeat} (written maatra {unclear.WrittenIndex + 1}).");

        long[] offsets = new long[timeline.Beats.Count];
        foreach (PlaybackBeat beat in timeline.Beats)
        {
            long gap = (beat.TaalBeat - 1 - elapsed % taal.MaatraCount + taal.MaatraCount) % taal.MaatraCount;
            elapsed += gap;
            offsets[beat.Position] = (elapsed - beat.Position) * Resolution;
            long tick = elapsed * Resolution;
            if (bols[beat.TaalBeat - 1] is string bol)
            {
                int[] strokes = DrumNotes(bol);
                for (int index = 0; index < strokes.Length; index++)
                {
                    long start = tick + index * Resolution / strokes.Length;
                    AddNote(drums, 10, strokes[index], beat.TaalBeat == 1 ? 102 : 82, start, start + Resolution / (4 * strokes.Length));
                }
            }
            elapsed++;
        }

        foreach (PlaybackNote note in timeline.Notes)
        {
            long start = Tick(note.Start) + offsets[note.Beat.Position];
            int lastBeat = (int)((note.End.Numerator - 1) / note.End.Denominator);
            long end = Tick(note.End) + offsets[lastBeat];
            if (end <= start)
                throw new InvalidDataException($"The note at written maatra {note.Beat.WrittenIndex + 1} is shorter than one MIDI tick.");
            AddNote(melody, 1, PitchConverter.GetMidiNote(note.Swara.Pitch!.Value, sa), 88, start, end);
            noteStarts?.Add(start, (note.Beat.SwaraSourceIndex, note.SwaraIndex));
        }
        melody.Add(new MetaEvent(MetaEventType.EndTrack, 0, elapsed * Resolution));
        drums.Add(new MetaEvent(MetaEventType.EndTrack, 0, elapsed * Resolution));
        using MemoryStream output = new();
        MidiFile.Export(output, file);
        return output.ToArray();
    }

    private static long Tick(PlaybackTime time)
        => checked((long)((time.Numerator * Resolution * 2 + time.Denominator) / (2 * time.Denominator)));

    private static void AddNote(IList<MidiEvent> track, int channel, int pitch, int velocity, long start, long end)
    {
        NoteOnEvent note = new(start, channel, pitch, velocity, checked((int)(end - start)));
        track.Add(note);
        track.Add(note.OffEvent);
    }

    // General MIDI drum approximation. Compound bols subdivide one maatra.
    private static int[] DrumNotes(string bol) => bol switch
    {
        "धा" => [36], "गे" => [41], "धिं" or "धी" or "दिन" => [45],
        "तिं" or "ती" or "तू" => [50], "ना" or "ता" => [38], "क" => [37],
        "तिरकिट" => [50, 38, 37, 38], "धागे" => [36, 41],
        "किट" => [37, 38], "तिट" => [50, 38], "कत" => [37, 38],
        "गदि" => [41, 45], "गन" => [41, 38],
        _ => throw new InvalidDataException($"No drum mapping for the bol {bol}.")
    };
}
