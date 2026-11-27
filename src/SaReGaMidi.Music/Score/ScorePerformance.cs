using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SaReGaMidi.Music.Rhythm;

namespace SaReGaMidi.Music.Score;

public static class ScorePerformance
{
    /// <summary>Builds a timeline for the supplied performance order. Time zero is the first performed maatra.</summary>
    public static PlaybackTimeline BuildTimeline(CompositionScore score, IReadOnlyList<ScorePass> order)
    {
        Taal taal = TaalCatalog.FindByName(score.TaalName) ?? throw new ArgumentException("Unknown taal.", nameof(score));
        var written = score.Sections.SelectMany(section => section.Maatras.Select((maatra, index) =>
            (Maatra: maatra, Section: section.Name, Beat: (section.StartBeat - 1 + index) % taal.MaatraCount + 1))).ToArray();
        List<PlaybackBeat> beats = [];
        List<PlaybackNote> notes = [];
        List<PlaybackBeat> unclear = [];
        int? sounding = null;

        foreach (ScorePass pass in order)
        {
            if (pass.StartMaatra < 0 || pass.MaatraCount < 1 || pass.StartMaatra > written.Length - pass.MaatraCount)
                throw new ArgumentOutOfRangeException(nameof(order), "A performance range falls outside the score.");

            for (int index = pass.StartMaatra; index < pass.StartMaatra + pass.MaatraCount; index++)
            {
                var cell = written[index];
                int source = cell.Maatra.BorrowedFrom ?? index;
                IReadOnlyList<SwaraEvent> swaras = written[source].Maatra.Swaras;
                PlaybackBeat beat = new(beats.Count, cell.Beat, cell.Section, index, source, cell.Maatra.Lyrics);
                beats.Add(beat);
                if (swaras.Count == 0)
                {
                    sounding = null;
                    continue;
                }

                MaatraNotation.Validate(swaras);
                PlaybackTime time = new(beat.Position, 1);
                foreach (SwaraEvent swara in swaras)
                {
                    PlaybackTime end = time.Add(swara.Duration);
                    switch (swara.Kind)
                    {
                        case SwaraEventKind.Note:
                            sounding = notes.Count;
                            notes.Add(new PlaybackNote(beat, swara, time, end));
                            break;
                        case SwaraEventKind.Sustain when sounding is int active:
                            notes[active] = notes[active] with { End = end };
                            break;
                        case SwaraEventKind.Unclear:
                            if (unclear.Count == 0 || unclear[^1] != beat)
                                unclear.Add(beat);
                            sounding = null;
                            break;
                        default:
                            sounding = null;
                            break;
                    }
                    time = end;
                }
            }
        }
        return new PlaybackTimeline(beats.AsReadOnly(), notes.AsReadOnly(), unclear.AsReadOnly());
    }

    public static IEnumerable<ScoreMaatra> ReadPass(CompositionScore score, ScorePass pass)
    {
        ScoreMaatra[] written = score.Sections.SelectMany(section => section.Maatras).ToArray();
        foreach (ScoreMaatra maatra in written.Skip(pass.StartMaatra).Take(pass.MaatraCount))
        {
            yield return maatra.BorrowedFrom is int source
                ? maatra with { Swaras = written[source].Swaras }
                : maatra;
        }
    }
}

public sealed record PlaybackTimeline(IReadOnlyList<PlaybackBeat> Beats, IReadOnlyList<PlaybackNote> Notes,
    IReadOnlyList<PlaybackBeat> UnclearBeats);

public sealed record PlaybackBeat(int Position, int TaalBeat, string Section, int WrittenIndex, int SwaraSourceIndex,
    string Lyrics);

public sealed record PlaybackNote(PlaybackBeat Beat, SwaraEvent Swara, PlaybackTime Start, PlaybackTime End);

/// <summary>Exact elapsed maatras from the start of this performance.</summary>
public readonly record struct PlaybackTime(BigInteger Numerator, BigInteger Denominator)
{
    internal PlaybackTime Add(MaatraFraction duration)
    {
        BigInteger denominator = Denominator * duration.Denominator;
        BigInteger numerator = Numerator * duration.Denominator + Denominator * duration.Numerator;
        BigInteger divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
        return new PlaybackTime(numerator / divisor, denominator / divisor);
    }
}