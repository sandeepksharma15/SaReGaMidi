namespace SaReGaMidi.Music.Notation;

public readonly record struct BhatkhandeSymbol(
    string Text,
    bool HasUnderline,
    bool HasVerticalStrokeAbove,
    bool HasDotBelow,
    bool HasDotAbove);
