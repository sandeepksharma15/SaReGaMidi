using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace SaReGaMidi.Music.Score;

/// <summary>Converts the editor's compact swara entry to and from timed events.</summary>
public static class MaatraNotation
{
    public static IReadOnlyList<SwaraEvent> Parse(string text)
    {
        string[] tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            throw new FormatException("Enter a swara, - for sustain, * for silence, or ? for an unclear reading.");

        List<SwaraEvent> events = new(tokens.Length);
        foreach (string token in tokens)
        {
            string[] parts = token.Split(':', 2);
            MaatraFraction duration = parts.Length == 2
                ? ParseDuration(parts[1])
                : new MaatraFraction(1, tokens.Length);

            events.Add(ParseEvent(parts[0], duration));
        }

        Validate(events);
        return events.AsReadOnly();
    }

    public static string Format(IReadOnlyList<SwaraEvent> events)
    {
        Validate(events);

        return string.Join(" ", events.Select(note =>
        {
            string symbol = note.Kind switch
            {
                SwaraEventKind.Note => FormatOrnaments(note),
                SwaraEventKind.Sustain => note.SustainPrintedAsS ? "S" : "-",
                SwaraEventKind.Silence => "*",
                _ => "?"
            };

            MaatraFraction duration = note.Duration;
            if ((long)duration.Numerator * events.Count == duration.Denominator)
                return symbol;

            return FormattableString.Invariant($"{symbol}:{duration.Numerator}/{duration.Denominator}");
        }));
    }

    public static void Validate(IReadOnlyList<SwaraEvent> events)
    {
        BigInteger numerator = BigInteger.Zero;
        BigInteger denominator = BigInteger.One;

        foreach (SwaraEvent note in events)
        {
            if (note is null || !Enum.IsDefined(note.Kind) || note.Kind == SwaraEventKind.LyricalContinuation
                || (note.SustainPrintedAsS && note.Kind != SwaraEventKind.Sustain)
                || (note.Kind == SwaraEventKind.Note) != note.Pitch.HasValue
                || (note.Pitch is SwaraPitch pitch && !IsPitchValid(pitch))
                || (note.Pitch is null && (note.Khatka || note.KanSwara.HasValue || note.MeendToNext
                    || note.AnchorId.HasValue || note.MeendTargetId.HasValue))
                || (note.MeendToNext && note.MeendTargetId.HasValue)
                || note.AnchorId == Guid.Empty || note.MeendTargetId == Guid.Empty
                || (note.KanSwara is SwaraPitch kan && !IsPitchValid(kan)))
            {
                throw new FormatException("The maatra contains an invalid swara event.");
            }

            MaatraFraction duration = note.Duration;
            if (duration.Numerator <= 0 || duration.Denominator <= 0 || duration.Numerator > duration.Denominator)
                throw new FormatException("Each swara duration must be positive and at most one maatra.");

            numerator = (numerator * duration.Denominator) + (duration.Numerator * denominator);
            denominator *= duration.Denominator;
            BigInteger divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
            numerator /= divisor;
            denominator /= divisor;
        }

        if (numerator != denominator)
            throw new FormatException("The swara durations must add up to one maatra.");
    }

    private static SwaraEvent ParseEvent(string symbol, MaatraFraction duration)
    {
        switch (symbol)
        {
            case "S":
                return new SwaraEvent(SwaraEventKind.Sustain, null, duration) { SustainPrintedAsS = true };
            case "-":
                return new SwaraEvent(SwaraEventKind.Sustain, null, duration);
            case "*":
                return new SwaraEvent(SwaraEventKind.Silence, null, duration);
            case "?":
                return new SwaraEvent(SwaraEventKind.Unclear, null, duration);
        }

        string[] anchorParts = symbol.Split('#', 2);
        Guid? anchorId = anchorParts.Length == 2 ? ParseAnchor(anchorParts[1]) : null;
        string[] meendParts = anchorParts[0].Split('~', 2);
        Guid? targetId = meendParts.Length == 2 && meendParts[1].Length > 0 ? ParseAnchor(meendParts[1]) : null;
        bool meend = meendParts.Length == 2 && !targetId.HasValue;
        symbol = meendParts[0];

        string[] pitches = symbol.Split('>', 2);
        string main = pitches[^1];
        bool khatka = main.StartsWith('(') && main.EndsWith(')');
        if (khatka)
            main = main[1..^1];

        return new SwaraEvent(SwaraEventKind.Note, ParsePitch(main), duration)
        {
            Khatka = khatka,
            KanSwara = pitches.Length == 2 ? ParsePitch(pitches[0]) : null,
            MeendToNext = meend,
            AnchorId = anchorId,
            MeendTargetId = targetId
        };
    }

    private static bool IsPitchValid(SwaraPitch pitch)
        => Enum.IsDefined(pitch.Swara) && Enum.IsDefined(pitch.Saptak);

    private static SwaraPitch ParsePitch(string symbol)
    {
        Saptak saptak = symbol.EndsWith('\'') ? Saptak.Taar : symbol.EndsWith('.') ? Saptak.Mandra : Saptak.Madhya;
        string swaraName = saptak == Saptak.Madhya ? symbol : symbol[..^1];

        if (!Enum.TryParse(swaraName, ignoreCase: true, out Swara swara)
            || !string.Equals(swaraName, Enum.GetName(swara), StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"Unknown swara '{symbol}'.");
        }

        return new SwaraPitch(swara, saptak);
    }

    private static string FormatOrnaments(SwaraEvent note)
    {
        string main = FormatPitch(note.Pitch!.Value);
        if (note.Khatka)
            main = $"({main})";

        if (note.KanSwara is SwaraPitch kan)
            main = $"{FormatPitch(kan)}>{main}";

        if (note.MeendTargetId is Guid target)
            main = $"{main}~{target:N}";
        else if (note.MeendToNext)
            main += "~";

        return note.AnchorId is Guid anchor ? $"{main}#{anchor:N}" : main;
    }

    private static Guid ParseAnchor(string text)
        => Guid.TryParseExact(text, "N", out Guid id) && id != Guid.Empty
            ? id : throw new FormatException("Invalid meend anchor.");

    private static string FormatPitch(SwaraPitch pitch)
    {
        string suffix = pitch.Saptak switch
        {
            Saptak.Mandra => ".",
            Saptak.Taar => "'",
            _ => string.Empty
        };

        return $"{pitch.Swara}{suffix}";
    }

    private static MaatraFraction ParseDuration(string text)
    {
        string[] parts = text.Split('/');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int numerator)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int denominator))
        {
            throw new FormatException($"Invalid duration '{text}'. Use a fraction such as 1/3.");
        }

        return new MaatraFraction(numerator, denominator);
    }
}
