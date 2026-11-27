using System;
using System.Collections.Generic;
using System.Linq;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

internal static class RanjishHiSahiDraft
{
    internal static bool Matches(IReadOnlyList<SourcePage> pages)
        => pages.Count == 3
            && string.Equals(pages[0].OriginalName, "Scan_20261127_105507.jpg", StringComparison.OrdinalIgnoreCase)
            && string.Equals(pages[1].OriginalName, "Scan_20261127_105522.jpg", StringComparison.OrdinalIgnoreCase)
            && string.Equals(pages[2].OriginalName, "Scan_20261127_105535.jpg", StringComparison.OrdinalIgnoreCase);

    internal static string DisplaySectionName(string name) => name.ToUpperInvariant() switch
    {
        "STHAYI" or "STHAAYI" => "स्थायी",
        "ANTARA" or "ANTRA" => "अंतरा",
        _ => name
    };

    internal static CompositionScore RestorePrintedLines(CompositionScore score)
    {
        if (!string.Equals(score.TaalName, "Dadra", StringComparison.OrdinalIgnoreCase)
            || score.Sections.Count != 2
            || DisplaySectionName(score.Sections[0].Name) != "स्थायी"
            || DisplaySectionName(score.Sections[1].Name) != "अंतरा"
            || score.Sections[0].StartBeat != 5 || score.Sections[1].StartBeat != 5
            || score.Sections[0].Maatras.Count != 56 || score.Sections[1].Maatras.Count != 74
            || score.Sections.SelectMany(section => section.Maatras).Any(maatra => maatra.StartsPrintedLine.HasValue))
        {
            return score;
        }

        IReadOnlyList<ScoreMaatra> sthayi = score.Sections[0].Maatras;
        bool knownPass = score.Passes.Any(pass => pass.StartMaatra == 30 && pass.MaatraCount == 26);
        bool knownCue = sthayi[26].Annotation.StartsWith("The first three melody cells on this printed line are blank.",
            StringComparison.Ordinal);
        if ((!knownPass && !knownCue) || sthayi[25].SourcePage != 2 || sthayi[30].SourcePage != 2)
            return score;

        return score with
        {
            Sections =
            [
                WithPrintedLines(score.Sections[0], [0, 8, 20, 26, 38, 50]),
                WithPrintedLines(score.Sections[1], [0, 8, 20, 32, 44, 56, 68])
            ]
        };
    }

    private static ScoreSection WithPrintedLines(ScoreSection section, int[] starts)
        => section with
        {
            Maatras = section.Maatras.Select((maatra, index) =>
                maatra with { StartsPrintedLine = starts.Contains(index) }).ToArray()
        };

    internal static CompositionScore RestorePasses(CompositionScore score)
    {
        if (score.Passes.Count != 0 || !string.Equals(score.TaalName, "Dadra", StringComparison.OrdinalIgnoreCase)
            || score.Sections.Count != 2 || DisplaySectionName(score.Sections[0].Name) != "स्थायी"
            || score.Sections[0].StartBeat != 5)
        {
            return score;
        }

        IReadOnlyList<ScoreMaatra> sthayi = score.Sections[0].Maatras;
        int setup = sthayi.ToList().FindIndex(maatra => maatra.Annotation.StartsWith(
            "The first three melody cells on this printed line are blank.", StringComparison.Ordinal));
        if (setup < 4 || setup + 6 >= sthayi.Count || (setup - 2) % 6 != 0
            || sthayi[setup - 2].SourcePage != 2 || sthayi[setup + 4].SourcePage != 2)
        {
            return score;
        }

        return score with { Passes = BuildPasses(setup - 2, setup + 4, sthayi.Count) };
    }

    private static IReadOnlyList<ScorePass> BuildPasses(int upperPickup, int lowerPickup, int sthayiEnd)
        => [new("पहली बार", 0, upperPickup), new("दूसरी बार", lowerPickup, sthayiEnd - lowerPickup)];

    internal static CompositionScore RestoreBorrowedPickup(CompositionScore score)
    {
        if (score.Passes.Count != 2 || score.Sections.Count == 0
            || DisplaySectionName(score.Sections[0].Name) != "स्थायी")
        {
            return score;
        }

        ScorePass second = score.Passes[1];
        ScoreSection sthayi = score.Sections[0];
        int lower = second.StartMaatra - 4;
        int upper = lower - 6;
        if (upper < 0 || second.StartMaatra + second.MaatraCount != sthayi.Maatras.Count
            || !sthayi.Maatras[lower].StartsPrintedLine.GetValueOrDefault()
            || (sthayi.StartBeat - 1 + lower) % 6 != 0
            || sthayi.Maatras.Skip(lower).Take(3).Any(maatra =>
                maatra.Swaras.Count != 1 || maatra.Swaras[0].Kind != SwaraEventKind.Unclear)
            || sthayi.Maatras.Skip(upper).Take(3).Any(maatra => maatra.Swaras.Count == 0))
        {
            return score;
        }

        ScoreMaatra[] maatras = sthayi.Maatras.ToArray();
        for (int beat = 0; beat < 3; beat++)
            maatras[lower + beat] = maatras[lower + beat] with { Swaras = [], BorrowedFrom = upper + beat };

        return score with
        {
            Sections = new[] { sthayi with { Maatras = maatras } }.Concat(score.Sections.Skip(1)).ToArray(),
            Passes = [score.Passes[0], second with { StartMaatra = lower, MaatraCount = second.MaatraCount + 4 }]
        };
    }

    internal static CompositionScore Create()
    {
        List<ScoreMaatra> sthayi = [];
        List<ScoreMaatra> antara = [];

        AddLine(sthayi, 1,
            "Sa;Dha.>Ni.;Sa;Ga MaTivra;Pa.;Re Re;Ga;-",
            "रं;S;जिश;हीS;S;Sस;ही;S",
            (0, "Opening pickup. Starts on Dadra maatra 5."),
            (1, "Check lower-register marks."));
        AddLine(sthayi, 1,
            "*;*;Ga;MaTivra Ga;Ga;MaTivra Dha;(Pa)~;MaTivra;MaTivra Re;Re;MaTivra;Dha",
            "*;*;दिल;हीS;दु;खाS;ने;S;के;लि;ए;S",
            (6, "Bracketed Pa (khatka). Meend from Pa toward Ma on the next maatra."),
            (8, "Check whether the stacked printed Ma–Re is a kan swar."));
        AddLine(sthayi, 2,
            "Pa;-;Ga Pa;Re Ga;Sa;Dha.>Ni.",
            "आ;S;SS;SS;रं;S",
            (5, "Printed return cue to the sthayi. Check the lower-register marks."));
        int upperPickup = sthayi.Count - 2;
        int lowerPickup = sthayi.Count + 4;
        AddLine(sthayi, 2,
            "?;?;?;-;Ni Sa';Dha Ni;Sa';Sa';Sa';Sa';Sa';-",
            "आ;S;S;S;आS;SS;फि;र;से;मु;झे;S",
            (0, "Printed context before the second-pass pickup. The first three melody cells are blank."),
            (4, "Second-pass pickup, Dadra maatra 5. Continue into the next line at sam."));
        AddLine(sthayi, 2,
            "Ni;Re' Ni;Ni;Dha;MaTivra;Dha Pa;Pa~;Re;Re;Re;Pa MaTivra;Dha Pa",
            "छो;SS;ड़;के;जा;SS;ने;S;के;लि;एS;SS",
            (6, "Meend from Pa to Re."));
        AddLine(sthayi, 2,
            "Pa;-;Ga Pa;Re Ga;Sa;Ni.",
            "आ;S;SS;SS;रं;S",
            (5, "Printed return cue to the sthayi."));

        AddLine(antara, 2,
            "Sa';Ni;Dha>Pa;Pa;MaTivra Ga;MaTivra;Ga Re;-",
            "कि;स;कि;स;कोS;ब;ताS;S",
            (0, "Antara pickup. Starts on Dadra maatra 5."),
            (2, "Printed Dha above Pa. Kan before Pa."));
        AddLine(antara, 2,
            "Ga;-;Ga~;Sa Sa;Ni.;Dha.;Ni.;Re;Re;Pa;Ga;Re Sa",
            "एं;S;गे;Sजु;दा;S;ई;S;का;स;ब;बS",
            (2, "Meend across the vibhaag line toward Sa."));
        AddLine(antara, 2,
            "Pa;-;Ga;Re Sa;Re MaTivra;MaTivra;MaTivra;MaTivra;MaTivra;MaTivra;MaTivra;Ni>Dha",
            "हम;S;S;SS;SS;किस;कि;स;को;ब;ता;S",
            (8, "Check the marks around the two Ma swaras on either side of the vibhaag line."),
            (11, "Printed Ni above Dha. Kan before Dha."));
        AddLine(antara, 2,
            "Ma>Ga;?;Re;Re;Re;-;Re;Ni;Ni;Dha;Dha;Pa",
            "एं;S;गे;जु;दा;S;ई;S;का;स;ब;ब",
            (0, "Printed Ma above Ga. Kan before Ga."),
            (1, "The swara row appears to use an S-shaped mark here. Its meaning needs review."));
        AddLine(antara, 3,
            "Pa;-;-;-;Ni Sa';Dha Ni;Sa';Sa';Sa';Sa';- Sa';-",
            "हम;S;S;S;तूS;SS;मु;झ;से;ख़;फा;S",
            (10, "Printed dash and Sa share a maatra. Check subdivision against the legend."));
        AddLine(antara, 3,
            "Ni;Re' Ni;Ni Dha;Dha;Pa;-;Re;-;Re;Re;MaTivra;Dha",
            "है;SS;तोS;ज़;मा;S;ने;S;के;लि;ए;S",
            (4, "Check whether the small mark beside Pa is intentional."));
        AddLine(antara, 3,
            "Pa;-;Ga Pa;Re Ga;Sa;Dha.>Ni.",
            "आ;S;SS;SS;रं;S",
            (5, "Printed return cue. Remaining verses are to follow the same pattern. Confirm arrangement before export."));

        return RestoreBorrowedPickup(new CompositionScore("रंजिश ही सही", "Dadra",
            [new ScoreSection("स्थायी", 5, sthayi.AsReadOnly()), new ScoreSection("अंतरा", 5, antara.AsReadOnly())])
        {
            Passes = BuildPasses(upperPickup, lowerPickup, sthayi.Count)
        });
    }

    private static void AddLine(List<ScoreMaatra> destination, int page, string swaras, string lyrics,
        params (int Index, string Text)[] notes)
    {
        string[] swaraCells = swaras.Split(';');
        string[] lyricCells = lyrics.Split(';');
        if (swaraCells.Length != lyricCells.Length)
            throw new InvalidOperationException("The reference draft contains a misaligned line.");

        for (int index = 0; index < swaraCells.Length; index++)
        {
            int annotation = Array.FindIndex(notes, note => note.Index == index);
            string text = annotation >= 0 ? notes[annotation].Text : string.Empty;
            destination.Add(new ScoreMaatra(page, MaatraNotation.Parse(swaraCells[index]), lyricCells[index], text)
            {
                StartsPrintedLine = index == 0
            });
        }
    }
}
