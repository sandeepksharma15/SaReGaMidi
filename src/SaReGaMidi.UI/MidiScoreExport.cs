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
        IDictionary<long, (int Maatra, int Swara)>? noteStarts = null, int rareCutoff = 5, bool immediateStart = false,
        bool smoothChords = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tempo, 20);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tempo, 400);
        Taal taal = TaalCatalog.FindByName(score.TaalName) ?? throw new InvalidDataException("Choose a taal.");
        string?[] bols = taal.Vibhaags.SelectMany(division => division.Theka).ToArray();
        MidiEventCollection file = new(1, Resolution);
        IList<MidiEvent> melody = file.AddTrack();
        IList<MidiEvent> drums = file.AddTrack();
        IList<MidiEvent> chords = file.AddTrack();
        IList<MidiEvent> bass = file.AddTrack();
        bass.Add(new TextEvent("Bass", MetaEventType.SequenceTrackName, 0));
        chords.Add(new TextEvent("Chords", MetaEventType.SequenceTrackName, 0));
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
        HashSet<int> sectionEntries = [];
        int phase = immediateStart ? timeline.Beats[0].TaalBeat - 1 : 0;
        foreach (PlaybackBeat beat in timeline.Beats)
        {
            long nextBeat = (elapsed + phase) % taal.MaatraCount;
            long gap = (beat.TaalBeat - 1 - nextBeat + taal.MaatraCount) % taal.MaatraCount;
            if (beat.Position > 0)
            {
                string previous = RanjishHiSahiDraft.DisplaySectionName(timeline.Beats[beat.Position - 1].Section.Trim());
                string current = RanjishHiSahiDraft.DisplaySectionName(beat.Section.Trim());
                if (previous == "स्थायी" && current is "अंतरा" or "अन्तरा")
                {
                    sectionEntries.Add(beat.Position);
                    // A rest or unarticulated pickup already leaves space for the theka.
                    if (gap == 0 && timeline.Notes.Any(n => n.Beat.Position < beat.Position
                        && n.End.Numerator >= beat.Position * n.End.Denominator)
                        && timeline.Notes.Any(n => n.Beat.Position == beat.Position
                            && n.Start.Numerator == beat.Position * n.Start.Denominator))
                        gap = taal.MaatraCount;
                }
                for (int step = 0; step < gap; step++)
                    WriteDrums(drums, bols, (int)((nextBeat + step) % taal.MaatraCount), (elapsed + step) * Resolution);
            }
            elapsed += gap;
            offsets[beat.Position] = (elapsed - beat.Position) * Resolution;
            WriteDrums(drums, bols, beat.TaalBeat - 1, elapsed * Resolution);
            elapsed++;
        }

        List<MelodyNote> notes = [];
        int[] scale = FrequentOffsets(score, rareCutoff);
        foreach (var division in PlanHarmony(taal, timeline, scale, sa, smoothChords))
        {
            long start = division.Start * (long)Resolution + offsets[division.Start];
            long end = division.End * (long)Resolution + offsets[division.Start];
            foreach (int pitch in division.Pitches)
            {
                int playable = pitch < 0 ? pitch + 12 : pitch > 127 ? pitch - 12 * ((pitch - 127 + 11) / 12) : pitch;
                AddNote(chords, 2, playable, 60, start, end);
            }
            if (bass.Count == 1) // Only the track name has been written.
                bass.Add(new PatchChangeEvent(start, 3, 32));
            long rootEnd = division.PlayFifth ? end - Resolution : end;
            AddNote(bass, 3, division.BassRoot, division.IsKhali ? 54 : 76, start, rootEnd);
            if (division.PlayFifth)
                AddNote(bass, 3, division.BassRoot + 7, 62, rootEnd, end);
        }
        foreach (PlaybackNote note in timeline.Notes)
        {
            long start = Tick(note.Start) + offsets[note.Beat.Position];
            int lastBeat = (int)((note.End.Numerator - 1) / note.End.Denominator);
            long end = Tick(note.End) + offsets[lastBeat];
            for (int pos = note.Beat.Position + 1; pos <= lastBeat; pos++)
                if (offsets[pos] != offsets[pos - 1] || sectionEntries.Contains(pos))
                {
                    end = pos * (long)Resolution + offsets[pos - 1];
                    break;
                }
            if (end <= start)
                throw new InvalidDataException($"The note at written maatra {note.Beat.WrittenIndex + 1} is shorter than one MIDI tick.");
            SwaraEvent principal = note.Swara;
            int pitch = PitchConverter.GetMidiNote(principal.Pitch!.Value, sa);
            MaatraFraction duration = principal.Duration;
            long writtenTicks = Math.Min(end - start,
                (long)Math.Round((double)Resolution * duration.Numerator / duration.Denominator));
            long writtenEnd = start + writtenTicks;
            if (principal.KanSwara is SwaraPitch kan)
            {
                // Take the grace from the written subdivision, even when a later sustain extends it.
                if (writtenTicks >= (principal.Khatka ? 5 : 2))
                    AddOrnament(PitchConverter.GetMidiNote(kan, sa), start + Math.Max(1, writtenTicks / 8), 68);
            }

            if (principal.Khatka)
            {
                int degree = (int)principal.Pitch.Value.Swara;
                int[] distances = scale.Select(offset => (offset - degree + 12) % 12).Where(distance => distance > 0).ToArray();
                if (distances.Length == 0)
                    throw new InvalidDataException("Khatka needs neighbouring scale notes. Check the swaras and rare-note cutoff in Analysis.");
                long turnStart = start;
                long length = writtenEnd - turnStart;
                if (length < 4)
                    throw new InvalidDataException($"The khatka in written maatra {note.Beat.WrittenIndex + 1} needs at least four MIDI ticks.");
                int upper = pitch + distances.Min();
                int lower = pitch + distances.Max() - 12;
                if (upper > 127 || lower < 0)
                    throw new InvalidDataException("The khatka falls outside the MIDI pitch range. Choose another Sa.");
                AddOrnament(pitch, turnStart + length / 4, 88);
                AddOrnament(upper, turnStart + length / 2, 88);
                AddOrnament(lower, turnStart + 3 * length / 4, 88);
            }
            noteStarts?.Add(start, (note.Beat.SwaraSourceIndex, note.SwaraIndex));
            notes.Add(new(principal, pitch, start, end));

            void AddOrnament(int midiPitch, long until, int velocity)
            {
                // The first attack receives an incoming glide. The final principal carries the outgoing one.
                SwaraEvent attack = principal with { MeendToNext = false, MeendTargetId = null };
                notes.Add(new(attack, midiPitch, start, until, velocity));
                noteStarts?.Add(start, (note.Beat.SwaraSourceIndex, note.SwaraIndex));
                principal = principal with { AnchorId = null };
                start = until;
            }
        }
        WriteMelody(melody, notes);
        melody.Add(new MetaEvent(MetaEventType.EndTrack, 0, elapsed * Resolution));
        drums.Add(new MetaEvent(MetaEventType.EndTrack, 0, elapsed * Resolution));
        chords.Add(new MetaEvent(MetaEventType.EndTrack, 0, elapsed * Resolution));
        bass.Add(new MetaEvent(MetaEventType.EndTrack, 0, elapsed * Resolution));
        using MemoryStream output = new();
        MidiFile.Export(output, file);
        return output.ToArray();
    }

    private static long Tick(PlaybackTime time)
        => checked((long)((time.Numerator * Resolution * 2 + time.Denominator) / (2 * time.Denominator)));

    private static void WriteDrums(IList<MidiEvent> drums, string?[] bols, int beat, long tick)
    {
        if (bols[beat] is not string bol)
            return;
        int[] strokes = DrumNotes(bol);
        for (int index = 0; index < strokes.Length; index++)
        {
            long start = tick + index * Resolution / strokes.Length;
            AddNote(drums, 10, strokes[index], beat == 0 ? 102 : 82, start, start + Resolution / (4 * strokes.Length));
        }
    }

    internal sealed record HarmonyDivision(int Start, int End, int WrittenIndex, SwaraPitch Principal,
        int Root, int[] Pitches, int BassRoot, bool PlayFifth, bool IsKhali)
    {
        internal HarmonyDivision[] Suggestions { get; init; } = [];

        internal ChordChoice RelativeTo(int sa) => new((Swara)Root, Pitches.Select(p => p - sa).ToArray());

        internal string Name(int sa)
        {
            string rootName = WesternNoteNames.Format(sa, (Swara)Root, false);
            var intervals = Pitches.Select(p => (p - sa - Root + 120) % 12).ToArray();
            if (intervals.Length == 1) return rootName + " (root)";
            if (intervals.Length == 2) return rootName + "5";
            if (intervals.Contains(6)) return rootName + "dim";
            if (intervals.Contains(8)) return rootName + "aug";
            return intervals.Contains(3) ? rootName + "m" : rootName;
        }
    }

    internal static IEnumerable<HarmonyDivision> PlanHarmony(Taal taal, PlaybackTimeline timeline,
        int[] scale, int sa, bool smooth)
    {
        Dictionary<int, Vibhaag> divisions = [];
        int firstBeat = 1;
        foreach (Vibhaag division in taal.Vibhaags)
        {
            divisions.Add(firstBeat, division);
            firstBeat += division.Theka.Count;
        }
        var attacks = timeline.Notes.ToLookup(note => note.Beat.Position);
        int[]? previous = null;
        for (int pos = 0; pos < timeline.Beats.Count; pos++)
        {
            if (!divisions.TryGetValue(timeline.Beats[pos].TaalBeat, out Vibhaag? division))
                continue;
            int length = division.Theka.Count;
            int end = pos + 1;
            while (end < timeline.Beats.Count && end < pos + length
                && timeline.Beats[end].TaalBeat == timeline.Beats[pos].TaalBeat + end - pos
                && timeline.Beats[end].Section == timeline.Beats[pos].Section)
                end++;
            PlaybackNote? first = Enumerable.Range(pos, end - pos).SelectMany(i => attacks[i]).FirstOrDefault();
            if (first is null)
                continue;
            int degree = (int)first.Swara.Pitch!.Value.Swara;
            var suggestions = RankChords(scale, degree, sa, smooth ? previous : null)
                .Select(candidate => MakeDivision(candidate.Root, candidate.Notes)).ToArray();
            ChordChoice? saved = timeline.Beats[pos].Chord;
            HarmonyDivision? selected = saved is null ? suggestions.FirstOrDefault()
                : MakeDivision((int)saved.Root, saved.Offsets.Select(offset => sa + offset).ToArray());
            if (selected is null)
                continue;
            yield return selected with { Suggestions = suggestions };
            previous = selected.Pitches;

            HarmonyDivision MakeDivision(int root, int[] chord)
            {
                int bassRoot = 28 + (sa + root - 28 + 120) % 12;
                bool playFifth = end - pos >= 3 && !division.IsKhali && chord.Any(p => (p - sa - root + 120) % 12 == 7);
                return new(pos, end, timeline.Beats[pos].WrittenIndex, first.Swara.Pitch.Value,
                    root, chord, bassRoot, playFifth, division.IsKhali);
            }
        }
    }

    private static IEnumerable<(int Root, int[] Notes)> RankChords(int[] scale, int degree, int sa, int[]? previous)
    {
        List<(int Root, int[] Notes)> candidates = [];
        foreach (int root in scale)
        {
            foreach ((int third, int fifth) in new[] { (4, 7), (3, 7), (3, 6), (4, 8) })
            {
                int[] tones = [root, root + third, root + fifth];
                if (!tones.All(t => scale.Contains(t % 12)))
                    continue;
                int[] pitches = tones.Select(t => sa - 12 + t).ToArray();
                for (int inversion = 0; inversion < (previous is null ? 1 : 3); inversion++)
                {
                    candidates.Add((root, pitches));
                    pitches = [pitches[1], pitches[2], pitches[0] + 12];
                }
            }
        }
        // Prefer a chord containing the attack. For an out-of-scale attack use the closest scale harmony.
        if (candidates.Count > 0)
        {
            var ranked = candidates.OrderBy(c => c.Notes.Min(p =>
            Math.Min((p - sa - degree + 120) % 12, (sa + degree - p + 120) % 12)))
                .ThenBy(c => previous is { Length: 3 } ? c.Notes.Zip(previous, (a, b) => Math.Abs(a - b)).Sum() : 0)
                .ThenBy(c => c.Root == degree ? 0 : 1);
            return ranked.DistinctBy(c => (c.Root, Mask: c.Notes.Sum(p => 1 << ((p + 12) % 12))))
                .Where((c, index) => index == 0 || c.Notes.Any(p => (p - sa - degree + 120) % 12 == 0)).Take(3);
        }
        // A sparse scale may have no complete triad. Use the available root and fifth if present.
        if (scale.Length == 0)
            return [];
        int bass = scale.Contains(degree) ? degree : scale[0];
        return [(bass, scale.Contains((bass + 7) % 12) ? [sa - 12 + bass, sa - 5 + bass] : [sa - 12 + bass])];
    }

    internal static int[] FrequentOffsets(CompositionScore score, int cutoff)
    {
        var usage = ScaleAnalysis.CountNotes(score.Sections.SelectMany(section => section.Maatras).SelectMany(cell => cell.Swaras));
        int total = usage.Sum(note => note.Count);
        return usage.Where(note => note.IsFrequent(total, cutoff)).Select(note => (int)note.Swara).ToArray();
    }

    private sealed record MelodyNote(SwaraEvent Swara, int Pitch, long Start, long End, int Velocity = 88);

    private static void WriteMelody(IList<MidiEvent> track, List<MelodyNote> notes)
    {
        int bendRange = 0;
        for (int index = 0; index < notes.Count; index++)
        {
            MelodyNote source = notes[index];
            int target = MeendEnd(notes, index);
            if (target == index)
            {
                AddNote(track, 1, source.Pitch, source.Velocity, source.Start, source.End);
                continue;
            }

            // A multi-maatra glide passes through each written pitch at its original onset.
            int range = 2;
            for (int next = index + 1; next <= target; next++)
            {
                if (next < target)
                    target = Math.Max(target, MeendEnd(notes, next));
                range = Math.Max(range, Math.Abs(notes[next].Pitch - source.Pitch));
            }
            if (range != bendRange)
            {
                SetBendRange(track, source.Start, range);
                bendRange = range;
            }
            AddNote(track, 1, source.Pitch, source.Velocity, source.Start, notes[target].Start);
            for (int next = index + 1; next <= target; next++)
            {
                MelodyNote from = notes[next - 1];
                MelodyNote to = notes[next];
                long duration = to.Start - from.Start;
                for (long tick = from.Start; tick < to.Start; tick += Resolution / 48)
                {
                    double fraction = (double)(tick - from.Start) / duration;
                    double offset = from.Pitch - source.Pitch + fraction * (to.Pitch - from.Pitch);
                    int bend = Math.Clamp(8192 + (int)Math.Round(offset * 8192 / range), 0, 16383);
                    track.Add(new PitchWheelChangeEvent(tick, 1, bend));
                }
            }
            track.Add(new PitchWheelChangeEvent(notes[target].Start, 1, 8192));
            index = target - 1; // Articulate the destination, which may start another glide.
        }
        if (bendRange > 2)
            SetBendRange(track, notes[^1].End, 2);
    }

    private static int MeendEnd(List<MelodyNote> notes, int start)
    {
        SwaraEvent source = notes[start].Swara;
        if (!source.MeendToNext && source.MeendTargetId is null)
            return start;
        for (int index = start + 1; index < notes.Count && notes[index - 1].End == notes[index].Start; index++)
        {
            if (source.MeendTargetId is Guid target ? notes[index].Swara.AnchorId == target : source.MeendToNext)
                return index;
        }
        return start;
    }

    private static void SetBendRange(IList<MidiEvent> track, long tick, int semitones)
    {
        // RPN 0 selects pitch bend sensitivity. Deselect it after setting semitones and cents.
        foreach ((int controller, int value) in new[] { (101, 0), (100, 0), (6, semitones), (38, 0), (101, 127), (100, 127) })
            track.Add(new ControlChangeEvent(tick, 1, (MidiController)controller, value));
    }

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
