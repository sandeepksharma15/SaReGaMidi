using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SaReGaMidi.Music.Score;

public sealed record CompositionScore(string Title, string TaalName, IReadOnlyList<ScoreSection> Sections)
{
    public IReadOnlyList<ScorePass> Passes { get; init; } = [];
}

/// <summary>A contiguous range of written maatras, indexed from zero across the composition.</summary>
public sealed record ScorePass(string Name, int StartMaatra, int MaatraCount);

/// <summary>Starts on the specified one-based maatra of the taal. Subsequent maatras advance through the cycle.</summary>
public sealed record ScoreSection(string Name, int StartBeat, IReadOnlyList<ScoreMaatra> Maatras);

/// <summary>A maatra and its separate lyric row. A source page is a one-based reference into the workspace manifest.</summary>
public sealed record ScoreMaatra(int SourcePage, IReadOnlyList<SwaraEvent> Swaras, string Lyrics, string Annotation)
{
    public const char LyricContinuationSign = 'S';

    public bool ShowGrouping { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChordChoice? Chord { get; init; }

    /// <summary>Starts a new printed line without changing the rhythm or performance order.</summary>
    public bool? StartsPrintedLine { get; init; }

    /// <summary>Zero-based written maatra supplying the swaras of an intentionally blank cell.</summary>
    public int? BorrowedFrom { get; init; }

    /// <summary>Non-timed lyric continuation markers, indexed by character position in Lyrics.</summary>
    [JsonIgnore]
    public IEnumerable<LyricMarker> LyricMarkers
    {
        get
        {
            for (int index = 0; index < Lyrics.Length; index++)
            {
                if (Lyrics[index] == LyricContinuationSign)
                    yield return new LyricMarker(index, SwaraEventKind.LyricalContinuation);
            }
        }
    }
}

public readonly record struct LyricMarker(int TextOffset, SwaraEventKind Kind);

/// <summary>A chord voicing relative to madhya Sa, attached to the written vibhaag start.</summary>
public sealed record ChordChoice(Swara Root, IReadOnlyList<int> Offsets);

public sealed record SwaraEvent(SwaraEventKind Kind, SwaraPitch? Pitch, MaatraFraction Duration)
{
    public bool SustainPrintedAsS { get; init; }

    public bool Khatka { get; init; }

    public SwaraPitch? KanSwara { get; init; }

    public bool MeendToNext { get; init; }

    public Guid? AnchorId { get; init; }

    public Guid? MeendTargetId { get; init; }
}

public readonly record struct MaatraFraction(int Numerator, int Denominator);

public enum SwaraEventKind
{
    Note,
    Sustain,
    Silence,
    Unclear,
    LyricalContinuation
}
