using System.Collections.Generic;

namespace SaReGaMidi.Music.Score;

public sealed record CompositionScore(string Title, string TaalName, IReadOnlyList<ScoreSection> Sections);

/// <summary>Starts on the specified one-based maatra of the taal. Subsequent maatras advance through the cycle.</summary>
public sealed record ScoreSection(string Name, int StartBeat, IReadOnlyList<ScoreMaatra> Maatras);

/// <summary>A maatra and its separate lyric row. A source page is a one-based reference into the workspace manifest.</summary>
public sealed record ScoreMaatra(int SourcePage, IReadOnlyList<SwaraEvent> Swaras, string Lyrics, string Annotation);

public sealed record SwaraEvent(SwaraEventKind Kind, SwaraPitch? Pitch, MaatraFraction Duration);

public readonly record struct MaatraFraction(int Numerator, int Denominator);

public enum SwaraEventKind
{
    Note,
    Sustain,
    Silence,
    Unclear
}
