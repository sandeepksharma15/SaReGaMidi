using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SaReGaMidi.Music.Rhythm;
using SaReGaMidi.Music.Score;
using SaReGaMidi.UI;
using Xunit;

namespace SaReGaMidi.Recognition.Tests;

public sealed class ScanRecognitionTests(ITestOutputHelper output)
{
    [Fact]
    public void ParseReadings_MissingCrop_PreservesPositionsAndLowestConfidence()
    {
        const string tsv = """
            level	page_num	block_num	par_num	line_num	word_num	left	top	width	height	conf	text
            5	1	1	1	1	1	0	0	10	10	97.5	सा
            5	1	1	1	1	2	0	0	10	10	68.25	रे
            1	2	0	0	0	0	0	0	10	10	-1
            5	3	1	1	1	1	0	0	10	10	93	ध
            """;

        OcrReading[] readings = TesseractOcr.ParseReadings(tsv, 3);

        Assert.Equal([new("सा रे", 68.25), new("", 0), new("ध", 93)], readings);
    }

    [Theory]
    [InlineData("सारे", "Sa Re")]
    [InlineData("(प) सां", "(Pa) Sa'")]
    [InlineData("म\u0951", "MaTivra")]
    [InlineData("रेम\u0951", "Re MaTivra")]
    [InlineData("–", "-")]
    [InlineData("S", "S")]
    [InlineData("ऽ", "S")]
    [InlineData("*", "*")]
    [InlineData("text", "?")]
    public void BuildMaatra_PrintedSymbols_PreservesSemantics(string printed, string expected)
    {
        ScoreMaatra maatra = ScanRecognition.BuildMaatra(2, true, new(printed, 96), true, new("हीऽ", 98), true, "");

        Assert.Equal(expected, MaatraNotation.Format(maatra.Swaras));
        Assert.Equal("हीS", maatra.Lyrics);
        Assert.Equal(expected == "?", maatra.Annotation.StartsWith("Review: ", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildMaatra_LyricOnlySlot_PreservesTheBlankForReview()
    {
        ScoreMaatra maatra = ScanRecognition.BuildMaatra(2, false, new("", 0), true, new("आ", 91), true, "");

        Assert.Empty(maatra.Swaras);
        Assert.Null(maatra.BorrowedFrom);
        Assert.Equal("आ", maatra.Lyrics);
        Assert.StartsWith("Review: ", maatra.Annotation);
    }

    [Theory]
    [InlineData("$", "S")]
    [InlineData("ही5", "हीS")]
    [InlineData("55५", "SSS")]
    [InlineData("5ही", "5ही")]
    public void BuildMaatra_LyricSuffix_ProposesContinuationMarks(string reading, string expected)
    {
        ScoreMaatra maatra = ScanRecognition.BuildMaatra(1, true, new("सा", 96), true, new(reading, 98), false, "");

        Assert.Equal(expected, maatra.Lyrics);
        Assert.Equal(reading != expected, maatra.Annotation.Contains("Confirm the lyric continuation marks.", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_RanjishHiSahiScans_MatchesPrintedLineLengthsAndPickups()
    {
        SourcePage[] pages = LoadReferenceScans();
        CompositionScore reference = RanjishHiSahiDraft.Create();
        Taal taal = TaalCatalog.FindByName(reference.TaalName)!;
        ScoreMaatra[][] expected = WrittenLines(reference).ToArray();
        List<PrintedLine> actual = [];

        for (int index = 0; index < pages.Length; index++)
        {
            IReadOnlyList<PrintedLine> lines = new PrintedNotationLayout(pages[index].Preview).Read(taal, CancellationToken.None);
            ScoreMaatra[][] pageLines = expected.Where(line => line[0].SourcePage == index + 1).ToArray();
            output.WriteLine($"Page {index + 1} has {lines.Count} printed lines and {lines.Sum(line => line.Cells.Count)} maatras.");
            Assert.Equal(pageLines.Select(line => line.Length), lines.Select(line => line.Cells.Count));
            actual.AddRange(lines);
        }

        Assert.Equal(reference.Sections[0].StartBeat, actual[0].StartBeat);
        Assert.Equal(reference.Sections[1].StartBeat, actual[6].StartBeat);
        Assert.All(actual[3].Cells.Take(3), cell =>
        {
            Assert.False(cell.HasSwaras);
            Assert.True(cell.HasLyrics);
        });
    }

    [Fact]
    public async Task GenerateAsync_RanjishHiSahiScans_ReportsRecognitionAgainstTheReference()
    {
        SourcePage[] pages = LoadReferenceScans();
        if (Environment.GetEnvironmentVariable("SAREGAMIDI_RUN_OCR_BENCHMARK") != "1")
            Assert.Skip("Set SAREGAMIDI_RUN_OCR_BENCHMARK=1 to run the local Tesseract benchmark.");

        CompositionScore reference = RanjishHiSahiDraft.Create();
        Taal taal = TaalCatalog.FindByName(reference.TaalName)!;
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));

        RecognitionDraft result = await ScanRecognition.GenerateAsync(pages, taal, reference.Title, timeout.Token);

        DirectoryInfo workspace = Directory.CreateTempSubdirectory("SaReGaMidi-Draft-");
        try
        {
            await new ScoreFile(workspace.FullName, pages.Length).SaveAsync(result.Score);
            CompositionScore? reopened = await new ScoreFile(workspace.FullName, pages.Length).LoadAsync();
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(result.Score),
                System.Text.Json.JsonSerializer.Serialize(reopened));
        }
        finally
        {
            workspace.Delete(recursive: true);
        }

        Assert.Empty(result.MissingPages);
        ScoreMaatra[][] expected = WrittenLines(reference).ToArray();
        ScoreMaatra[][] actual = WrittenLines(result.Score).ToArray();
        Assert.Equal(expected.Select(line => line.Length), actual.Select(line => line.Length));
        (ScoreMaatra Expected, ScoreMaatra Actual)[] cells = expected.SelectMany(line => line)
            .Zip(actual.SelectMany(line => line)).ToArray();
        int swaras = cells.Count(pair => pair.Expected.Swaras.SequenceEqual(pair.Actual.Swaras));
        int lyrics = cells.Count(pair => pair.Expected.Lyrics == pair.Actual.Lyrics);
        int continuationCells = cells.Count(pair => pair.Expected.Lyrics.Contains(ScoreMaatra.LyricContinuationSign));
        int correctContinuationCells = cells.Count(pair => pair.Expected.Lyrics.Contains(ScoreMaatra.LyricContinuationSign)
            && ContinuationEdges(pair.Expected.Lyrics) == ContinuationEdges(pair.Actual.Lyrics));
        int continuations = cells.Count(pair => pair.Expected.Lyrics.Contains(ScoreMaatra.LyricContinuationSign)
            && pair.Expected.Lyrics == pair.Actual.Lyrics);
        int exact = cells.Count(pair => pair.Expected.Swaras.SequenceEqual(pair.Actual.Swaras)
            && pair.Expected.Lyrics == pair.Actual.Lyrics);
        output.WriteLine($"Exact timed swaras {swaras}/{cells.Length}, lyrics {lyrics}/{cells.Length}, "
            + $"both {exact}/{cells.Length}, flagged {result.ReviewCount}/{cells.Length}.");
        output.WriteLine($"Correct continuation placement and count {correctContinuationCells}/{continuationCells}, "
            + $"exact lyrics in those cells {continuations}/{continuationCells}.");
        output.WriteLine($"Sections: {string.Join(", ", result.Score.Sections.Select(section => $"{section.Name} (beat {section.StartBeat})"))}.");
        for (int index = 0; index < cells.Length; index++)
        {
            (ScoreMaatra original, ScoreMaatra draft) = cells[index];
            string originalNotes = original.Swaras.Count == 0 ? "" : MaatraNotation.Format(original.Swaras);
            string draftNotes = draft.Swaras.Count == 0 ? "" : MaatraNotation.Format(draft.Swaras);
            if (originalNotes != draftNotes || draftNotes.Contains('>') || draftNotes.Contains('~'))
                output.WriteLine($"Maatra {index + 1} expected '{originalNotes}', read '{draftNotes}'.");
            if (original.Lyrics != draft.Lyrics)
                output.WriteLine($"Maatra {index + 1} expected lyric '{original.Lyrics}', read '{draft.Lyrics}'.");
        }
        Assert.Equal(reference.Sections.Select(section => (section.Name, section.StartBeat)),
            result.Score.Sections.Select(section => (section.Name, section.StartBeat)));
        Assert.Equal(cells.Select(pair => ContinuationEdges(pair.Expected.Lyrics)),
            cells.Select(pair => ContinuationEdges(pair.Actual.Lyrics)));
        Assert.Equal("Dha.>Ni.", MaatraNotation.Format(cells[1].Actual.Swaras));
        Assert.Equal("Ga MaTivra", MaatraNotation.Format(cells[3].Actual.Swaras));
        int[] recoveredLyrics = [9, 10, 13, 39, 68, 95, 105, 110, 116];
        foreach (int number in recoveredLyrics)
            Assert.Equal(cells[number - 1].Expected.Lyrics, cells[number - 1].Actual.Lyrics);

        int[] recoveredSwaras = [14, 26, 50, 51, 56, 59, 69, 70, 71, 75, 76, 85, 86, 90];
        foreach (int number in recoveredSwaras)
            Assert.Equal<SwaraEvent>(cells[number - 1].Expected.Swaras, cells[number - 1].Actual.Swaras);

        int[] meendStarts = cells.Select((pair, index) => (pair, index))
            .Where(entry => entry.pair.Actual.Swaras.Any(swara => swara.MeendToNext)).Select(entry => entry.index).ToArray();
        Assert.Equal(cells.Select((pair, index) => (pair, index))
            .Where(entry => entry.pair.Expected.Swaras.Any(swara => swara.MeendToNext)).Select(entry => entry.index), meendStarts);
    }

    [Fact]
    public async Task GenerateAsync_KeharwaScans_PreservesThePageTurnAndBeatGrid()
    {
        string? folder = Environment.GetEnvironmentVariable("SAREGAMIDI_REFERENCE_SCANS");
        string[] names = ["Scan_20261127_153314.jpg", "Scan_20261127_153339.jpg"];
        if (folder is null || names.Any(name => !File.Exists(Path.Combine(folder, name)))
            || Environment.GetEnvironmentVariable("SAREGAMIDI_RUN_OCR_BENCHMARK") != "1")
            Assert.Skip("Requires the local Keharwa scans and OCR benchmark.");

        SourcePage[] pages = names.Select(name => SourcePage.Load(Path.Combine(folder, name))).ToArray();
        RecognitionDraft draft = await ScanRecognition.GenerateAsync(pages, TaalCatalog.FindByName("Keharwa")!,
            "अपनी धुन में रहता हूँ", CancellationToken.None);
        ScoreMaatra[][] lines = WrittenLines(draft.Score).ToArray();

        foreach (ScoreMaatra[] line in lines)
            output.WriteLine(string.Join(" | ", line.Select(cell => cell.Swaras.Count == 0 ? "" : MaatraNotation.Format(cell.Swaras))));

        Assert.Empty(draft.MissingPages);
        Assert.Equal([16, 16, 16, 16, 16, 16, 8], lines.Select(line => line.Length));
        Assert.Equal(["स्थायी", "अंतरा"], draft.Score.Sections.Select(section => section.Name));
        Assert.Equal([48, 56], draft.Score.Sections.Select(section => section.Maatras.Count));
    }

    [Fact]
    public async Task GenerateAsync_KeharwaRescan_ReadsCrowdedSwaras()
    {
        string? folder = Environment.GetEnvironmentVariable("SAREGAMIDI_REFERENCE_SCANS");
        string[] names = ["Scan_20261127_153314.jpg", "Scan_20261127_172451.jpg"];
        if (folder is null || names.Any(name => !File.Exists(Path.Combine(folder, name)))
            || Environment.GetEnvironmentVariable("SAREGAMIDI_RUN_OCR_BENCHMARK") != "1")
            Assert.Skip("Requires the local Keharwa rescan and OCR benchmark.");

        SourcePage[] pages = names.Select(name => SourcePage.Load(Path.Combine(folder, name))).ToArray();
        Taal taal = TaalCatalog.FindByName("Keharwa")!;
        RecognitionDraft draft = await ScanRecognition.GenerateAsync(pages, taal, "Keharwa", CancellationToken.None);
        ScoreMaatra[][] lines = WrittenLines(draft.Score).ToArray();
        foreach (ScoreMaatra[] line in lines)
            output.WriteLine(string.Join(" | ", line.Select(cell => MaatraNotation.Format(cell.Swaras))));
        Assert.Equal("GaKomal ReKomal", MaatraNotation.Format(lines[0][12].Swaras));
        Assert.Equal("Sa'", MaatraNotation.Format(lines[3][2].Swaras));
        Assert.Equal("GaKomal' Ma'", MaatraNotation.Format(lines[4][13].Swaras));
        Assert.Equal("NiKomal", MaatraNotation.Format(lines[5][1].Swaras));
        Assert.Equal("NiKomal DhaKomal", MaatraNotation.Format(lines[5][0].Swaras));
        Assert.Equal([16, 16, 16, 16, 16, 16, 8], lines.Select(line => line.Length));
        Assert.DoesNotContain(lines.SelectMany(line => line).SelectMany(cell => cell.Swaras), note => note.Kind == SwaraEventKind.Unclear);
    }

    private static (int Leading, int Trailing) ContinuationEdges(string lyrics)
    {
        char sign = ScoreMaatra.LyricContinuationSign;
        int leading = lyrics.Length - lyrics.TrimStart(sign).Length;
        int trailing = lyrics.Length - lyrics.TrimEnd(sign).Length;
        return (leading == lyrics.Length ? 0 : leading, trailing);
    }

    private static SourcePage[] LoadReferenceScans()
    {
        string? folder = Environment.GetEnvironmentVariable("SAREGAMIDI_REFERENCE_SCANS");
        if (string.IsNullOrWhiteSpace(folder))
            Assert.Skip("Set SAREGAMIDI_REFERENCE_SCANS to the folder containing the three original scans.");

        string[] names = ["Scan_20261127_105507.jpg", "Scan_20261127_105522.jpg", "Scan_20261127_105535.jpg"];
        return names.Select(name => SourcePage.Load(Path.Combine(folder!, name))).ToArray();
    }

    private static IEnumerable<ScoreMaatra[]> WrittenLines(CompositionScore score)
    {
        foreach (ScoreSection section in score.Sections)
        {
            List<ScoreMaatra> line = [];
            foreach (ScoreMaatra maatra in section.Maatras)
            {
                if (maatra.StartsPrintedLine == true && line.Count > 0)
                {
                    yield return line.ToArray();
                    line.Clear();
                }

                line.Add(maatra);
            }

            if (line.Count > 0)
                yield return line.ToArray();
        }
    }
}
