using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SaReGaMidi.Music.Rhythm;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

internal static class ScanRecognition
{
    private const double LowConfidence = 65;
    private static readonly Regex _continuationSuffix = new(@"[S5५$ऽƧ]+$", RegexOptions.CultureInvariant);
    private static readonly (string Printed, string Notation)[] _swaraNames =
    [
        ("सा", "Sa"), ("रे", "Re"), ("नि", "Ni"), ("ग", "Ga"), ("म", "Ma"),
        ("प", "Pa"), ("ध", "Dha")
    ];

    internal static async Task<RecognitionDraft> GenerateAsync(IReadOnlyList<SourcePage> pages, Taal taal,
        string title, CancellationToken cancellationToken)
    {
        if (taal.Vibhaags.Select(division => division.Theka.Count).Distinct().Count() != 1)
            throw new InvalidOperationException("This prototype reads taals with equal-length vibhaags, such as Dadra.");

        List<ScoreSection> sections = [];
        List<ScoreMaatra> maatras = [];
        List<int> missingPages = [];
        string section = "स्थायी";
        int startBeat = 1;
        int reviewCount = 0;

        for (int page = 0; page < pages.Count; page++)
        {
            SourcePage source = pages[page];
            IReadOnlyList<PrintedLine> layout = new PrintedNotationLayout(source.Preview).Read(taal, cancellationToken);
            if (layout.Count == 0)
            {
                missingPages.Add(page + 1);
                continue;
            }

            List<Int32Rect> crops = [];
            SwaraInk ink = new(source.Preview);
            List<(PrintedLine Line, int? Heading, CellCrops[] Cells)> rows = [];
            int bottom = 0;
            foreach (PrintedLine line in layout)
            {
                int width = source.Preview.PixelWidth;
                int headingTop = Math.Max(bottom + 8, line.Top - width / 7);
                int height = line.Top - width / 30 - headingTop;
                int? heading = height >= 20
                    ? AddCrop(new Int32Rect(width * 3 / 8, headingTop, width / 4, height))
                    : null;
                int?[] arcs = new int?[line.Cells.Count];
                for (int index = 0; index + 1 < line.Cells.Count; index++)
                    arcs[index] = ink.FindMeendBottom(line.Cells[index].Swaras, line.Cells[index + 1].Swaras);

                CellCrops[] cells = new CellCrops[line.Cells.Count];
                for (int index = 0; index < cells.Length; index++)
                {
                    PrintedCell cell = line.Cells[index];
                    SwaraMarks marks = ink.Read(cell.Swaras);
                    string? symbol = cell.HasSustainStroke ? "-" : marks.Symbol;
                    int clippedTop = Math.Max(arcs[index] ?? 0, index > 0 ? arcs[index - 1] ?? 0 : 0);
                    Int32Rect cleaned = cell.Swaras;
                    if (clippedTop > cleaned.Y && clippedTop - cleaned.Y < cleaned.Height / 3)
                    {
                        cleaned.Height -= clippedTop - cleaned.Y;
                        cleaned.Y = clippedTop;
                    }

                    LyricMarks lyricMarks = cell.HasLyrics ? ink.ReadLyricMarks(cell.Lyrics) : default;
                    bool lyricSilence = symbol == "*" && cell.HasLyrics && ink.IsLyricSilence(cell.Lyrics);
                    Int32Rect? lyricBody = lyricSilence ? null : lyricMarks.Body
                        ?? (cell.HasLyrics && !lyricMarks.Found ? cell.Lyrics : null);
                    int? tightSwara = null;
                    int? unmarkedMa = null;
                    bool distantInk = false;
                    if (cell.HasSwaras && symbol is null)
                    {
                        Int32Rect tighter = ink.Tighten(cleaned, out distantInk);
                        tightSwara = AddCrop(tighter);
                        if (marks.Tivra && marks.Fallback.HasValue)
                        {
                            // With a visible tivra stroke, try reading the swara without its upper marks.
                            int upperMargin = tighter.Height / 5;
                            unmarkedMa = AddCrop(new Int32Rect(tighter.X, tighter.Y + upperMargin,
                                tighter.Width, tighter.Height - upperMargin));
                        }
                    }

                    cells[index] = new CellCrops(
                        cell.HasSwaras && symbol is null ? AddCrop(cleaned) : null,
                        !lyricSilence && cell.HasLyrics && (!lyricMarks.Found || lyricMarks.Body.HasValue)
                            ? AddCrop(cell.Lyrics) : null,
                        lyricMarks.Body is Int32Rect body ? AddCrop(body) : null,
                        lyricBody is Int32Rect bodyBounds ? AddCrop(ink.Tighten(bodyBounds, out _)) : null,
                        lyricMarks,
                        lyricSilence,
                        lyricBody is Int32Rect lyricBounds && ink.HasLyricNukta(lyricBounds),
                        tightSwara, unmarkedMa, distantInk,
                        marks.Fallback is Int32Rect fallback && symbol is null ? AddCrop(fallback) : null,
                        marks.Kan is Int32Rect kan ? AddCrop(kan) : null,
                        marks.Main is Int32Rect main ? AddCrop(main) : null,
                        marks, arcs[index].HasValue, symbol);
                }

                rows.Add((line, heading, cells));
                bottom = line.Bottom;
            }

            OcrReading[] readings = await TesseractOcr.ReadAsync(source.Preview, crops, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                string? heading = ReadHeading(ReadCrop(row.Heading).Text);
                if (heading is not null && heading != section && maatras.Count > 0)
                {
                    sections.Add(new ScoreSection(section, startBeat, maatras));
                    maatras = [];
                }

                if (maatras.Count == 0)
                {
                    section = heading ?? section;
                    startBeat = row.Line.StartBeat;
                }

                for (int index = 0; index < row.Cells.Length; index++)
                {
                    CellCrops cell = row.Cells[index];
                    string cue = index != 0 ? string.Empty : maatras.Count == 0
                        ? "Confirm the section and its starting beat. Check printed marks and return cues."
                        : "Check this line's alignment and printed marks, including repeat and return cues.";
                    OcrReading swaraReading = cell.Symbol is string symbol ? new(symbol, 100) : ReadCrop(cell.Swaras);
                    OcrReading tightSwara = ReadCrop(cell.TightSwara);
                    OcrReading isolated = ReadCrop(cell.Fallback);
                    if (tightSwara.Text.EndsWith("म॑", StringComparison.Ordinal)
                        && isolated.Text is "म" or "मं" && ReadSwaras(NormalizeSwara(swaraReading.Text)).Length == 0)
                        swaraReading = new("म॑", tightSwara.Confidence);
                    if ((cell.DistantInk || swaraReading.Confidence < LowConfidence) && tightSwara.Confidence >= LowConfidence
                        && ReadSwaras(NormalizeSwara(tightSwara.Text), cell.Marks.Tivra).Length > 0)
                        swaraReading = tightSwara;

                    OcrReading unmarkedMa = ReadCrop(cell.UnmarkedMa);
                    if (unmarkedMa.Confidence >= LowConfidence && unmarkedMa.Text == "म"
                        && ReadSwaras(NormalizeSwara(swaraReading.Text), cell.Marks.Tivra).Length == 0)
                        swaraReading = new("म॑", swaraReading.Confidence);

                    OcrReading lyrics = cell.LyricSilence ? new("*", 100) : ReadLyrics(ReadCrop(cell.Lyrics),
                        ReadCrop(cell.LyricBody), ReadCrop(cell.TightLyric), cell.LyricMarks, cell.LyricNukta);
                    ScoreMaatra maatra = BuildMaatra(page + 1, cell.Swaras.HasValue || cell.Symbol is not null,
                        swaraReading, cell.Lyrics.HasValue || cell.LyricMarks.Found || cell.LyricSilence,
                        lyrics, index == 0, cue,
                        ReadCrop(cell.Kan), ReadCrop(cell.Main), cell.Meend, isolated, cell.Marks);
                    if (maatra.Annotation.Length > 0)
                        reviewCount++;

                    maatras.Add(maatra);
                }

                for (int index = maatras.Count - row.Cells.Length; index + 1 < maatras.Count; index++)
                {
                    ScoreMaatra current = maatras[index];
                    if (current.Swaras is [SwaraEvent { MeendToNext: true } swara]
                        && !maatras[index + 1].Swaras.Any(next => next.Pitch.HasValue))
                    {
                        maatras[index] = current with { Swaras = [swara with { MeendToNext = false }] };
                    }
                }
            }

            int AddCrop(Int32Rect rect)
            {
                crops.Add(rect);
                return crops.Count - 1;
            }

            OcrReading ReadCrop(int? index) => index is int position ? readings[position] : new("", 0);
        }

        if (maatras.Count == 0)
            throw new InvalidDataException("No notation rows were found. Try clearer scans with visible vibhaag dividers.");

        sections.Add(new ScoreSection(section, startBeat, maatras));
        CompositionScore score = new(string.IsNullOrWhiteSpace(title) ? "Untitled composition" : title.Trim(),
            taal.Name, sections);
        return new RecognitionDraft(score, reviewCount, missingPages);
    }

    private static OcrReading ReadLyrics(OcrReading original, OcrReading body, OcrReading tight,
        LyricMarks marks, bool nukta)
    {
        if (!marks.Found)
        {
            OcrReading reading = (original.Confidence < LowConfidence
                && tight.Confidence > original.Confidence + 15
                && (!tight.Text.Contains('\u094D') || original.Text.Contains('\u094D') || original.Text.Length == 0)
                && tight.Text.Any(IsDevanagariLetter)) ? tight : original;
            return CorrectLyricNukta(reading, nukta);
        }

        char sign = ScoreMaatra.LyricContinuationSign;
        string leading = new(sign, marks.Leading);
        string trailing = new(sign, marks.Trailing);
        if (!marks.Body.HasValue)
            return new(leading + trailing, 100);

        string text = original.Text.Trim();
        // A continuation touching the headline can look like the first consonant of a conjunct.
        // Recover the syllable when the two isolated readings corroborate its consonant and vowel.
        if (marks.Leading > 0 && marks.Trailing == 0 && text.Length == 4 && text[1] == '्'
            && IsDevanagariLetter(text[2]) && text[3] is >= 'ा' and <= 'ौ'
            && tight.Text.Length == 2 && body.Text.Length == 2
            && tight.Text[0] == text[2] && body.Text[1] == text[3])
            return new(leading + text[2..], Math.Min(body.Confidence, tight.Confidence));

        Match suffix = _continuationSuffix.Match(text);
        int end = suffix.Success ? suffix.Index : text.Length;
        int start = text.Length - text.TrimStart(sign).Length;
        if (tight.Text.Any(IsDevanagariLetter) && (tight.Text == text
            || (body.Confidence < LowConfidence && tight.Confidence > body.Confidence + 15)))
            body = tight;
        if (start == marks.Leading && suffix.Length == marks.Trailing && start < end
            && original.Confidence >= body.Confidence)
            body = new(text[start..end], original.Confidence);

        if (body.Text.Length > 0)
        {
            body = CorrectLyricNukta(body, nukta);
            return new(leading + body.Text.Trim() + trailing, body.Confidence);
        }

        return new(leading + text[Math.Min(start, end)..end] + trailing, 0);
    }

    private static bool IsDevanagariLetter(char character) => character is >= '\u0904' and <= '\u0939';

    private static OcrReading CorrectLyricNukta(OcrReading reading, bool nukta)
    {
        if (nukta && reading.Text is "क" or "ख" or "ग" or "ज" or "ड" or "ढ" or "फ")
            return reading with { Text = reading.Text + '\u093C' };
        return reading;
    }

    internal static ScoreMaatra BuildMaatra(int page, bool hasSwaras, OcrReading swaraReading,
        bool hasLyrics, OcrReading lyricReading, bool startsLine, string lineCue,
        OcrReading kan = default, OcrReading main = default, bool meend = false, OcrReading fallback = default,
        SwaraMarks? marks = null)
    {
        List<string> cues = [];
        if (lineCue.Length > 0)
            cues.Add(lineCue);

        IReadOnlyList<SwaraEvent> events = [];
        if (hasSwaras)
        {
            bool tivra = marks?.Tivra == true;
            string notation = ReadSwaras(NormalizeSwara(swaraReading.Text), tivra);
            if (swaraReading.Text.Contains('घ'))
                cues.Add("Confirm the Dha reading.");
            if (notation.Length == 0 && fallback.Text is { Length: > 0 })
            {
                // A halant belongs to a consonant cluster in text and cannot qualify a standalone swara.
                string candidate = ReadSwaras(NormalizeSwara(fallback.Text.TrimEnd('\u200C').TrimEnd('\u094D')), tivra);
                if (IsSinglePitch(candidate))
                {
                    notation = candidate;
                    cues.Add("Confirm the isolated swara reading.");
                }
            }
            if (kan.Text is { Length: > 0 })
            {
                string kanPitch = ReadSwaras(NormalizeSwara(kan.Text));
                string mainPitch = ReadSwaras(main.Text ?? string.Empty);
                if (mainPitch.Length == 0)
                    mainPitch = notation;
                if (IsSinglePitch(kanPitch) && IsSinglePitch(mainPitch))
                {
                    notation = $"{kanPitch}{marks?.KanOctave}>{mainPitch}{marks?.MainOctave}";
                    cues.Add("Confirm the kan swar and its octave marks.");
                }
            }

            if (marks?.Octave is char octave)
            {
                int lastSpace = notation.LastIndexOf(' ');
                string lastPitch = notation[(lastSpace + 1)..];
                if (IsSinglePitch(lastPitch) && (lastSpace < 0 || octave == '\''))
                    notation += octave;
            }

            if (meend)
            {
                if (IsSinglePitch(notation.TrimEnd('.', '\'')) || (notation.StartsWith('(') && notation.EndsWith(')')))
                    notation += "~";
                cues.Add("Check the printed meend and its endpoints.");
            }

            events = MaatraNotation.Parse(notation.Length == 0 ? "?" : notation);
            if (notation.Length == 0 || swaraReading.Confidence < LowConfidence)
                cues.Add("Check the swara reading and printed marks.");
        }
        else if (hasLyrics)
        {
            cues.Add("Confirm the blank swara cell against the scan.");
        }

        string lyrics = lyricReading.Text.Replace('ऽ', ScoreMaatra.LyricContinuationSign)
            .Replace('Ƨ', ScoreMaatra.LyricContinuationSign).Trim();
        Match suffix = _continuationSuffix.Match(lyrics);
        if (suffix.Success && suffix.Value.Any(character => character != ScoreMaatra.LyricContinuationSign))
        {
            lyrics = lyrics[..suffix.Index] + new string(ScoreMaatra.LyricContinuationSign, suffix.Length);
            cues.Add("Confirm the lyric continuation marks.");
        }
        if (hasLyrics && (lyrics.Length == 0 || lyricReading.Confidence < LowConfidence))
            cues.Add("Check the lyric reading.");

        return new ScoreMaatra(page, events, lyrics, cues.Count == 0 ? string.Empty : $"Review: {string.Join(" ", cues)}")
        {
            StartsPrintedLine = startsLine
        };
    }

    private static bool IsSinglePitch(string notation)
        => _swaraNames.Any(name => name.Notation == notation) || notation == "MaTivra";

    private static string NormalizeSwara(string text)
        => text.Replace('घ', 'ध').Replace("ग़", "ग", StringComparison.Ordinal);

    private static string ReadSwaras(string text, bool tivra = false)
    {
        string printed = string.Concat(text.Normalize(NormalizationForm.FormC).Where(character => !char.IsWhiteSpace(character)));
        List<string> tokens = [];
        for (int index = 0; index < printed.Length;)
        {
            char mark = printed[index];
            if (mark is '-' or '–' or '—' or '*' or '∗')
            {
                tokens.Add(mark is '*' or '∗' ? "*" : "-");
                index++;
                continue;
            }

            bool khatka = mark == '(';
            if (khatka)
                index++;

            (string glyph, string token) = Array.Find(_swaraNames, item =>
                printed.AsSpan(index).StartsWith(item.Printed, StringComparison.Ordinal));
            if (glyph is null)
                return string.Empty;

            index += glyph.Length;
            if (token == "Ma")
            {
                if (index < printed.Length && printed[index] == '\u0951')
                {
                    token = "MaTivra";
                    index++;
                }
                else if (tivra && printed.Count(character => character == 'म') == 1)
                {
                    token = "MaTivra";
                }
            }
            if (index < printed.Length && printed[index] == 'ं')
            {
                index++;
                if (token != "MaTivra")
                    token += "'";
            }

            if (khatka)
            {
                if (index == printed.Length || printed[index++] != ')')
                    return string.Empty;
                token = $"({token})";
            }

            tokens.Add(token);
        }

        return string.Join(" ", tokens);
    }

    private static string? ReadHeading(string text)
    {
        string heading = string.Concat(text.Where(character => !char.IsWhiteSpace(character)));
        if (heading.StartsWith("स्थायी", StringComparison.Ordinal))
            return "स्थायी";
        return heading is "अंतरा" or "अन्तरा" ? "अंतरा" : null;
    }

    private sealed record CellCrops(int? Swaras, int? Lyrics, int? LyricBody, int? TightLyric, LyricMarks LyricMarks,
        bool LyricSilence, bool LyricNukta, int? TightSwara, int? UnmarkedMa, bool DistantInk,
        int? Fallback, int? Kan, int? Main, SwaraMarks Marks, bool Meend,
        string? Symbol);
}

internal sealed record RecognitionDraft(CompositionScore Score, int ReviewCount, IReadOnlyList<int> MissingPages);
