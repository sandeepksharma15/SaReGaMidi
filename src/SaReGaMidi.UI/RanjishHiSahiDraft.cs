using System;
using System.Collections.Generic;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

internal static class RanjishHiSahiDraft
{
    internal static bool Matches(IReadOnlyList<SourcePage> pages)
        => pages.Count == 3
            && string.Equals(pages[0].OriginalName, "Scan_20261127_105507.jpg", StringComparison.OrdinalIgnoreCase)
            && string.Equals(pages[1].OriginalName, "Scan_20261127_105522.jpg", StringComparison.OrdinalIgnoreCase)
            && string.Equals(pages[2].OriginalName, "Scan_20261127_105535.jpg", StringComparison.OrdinalIgnoreCase);

    internal static CompositionScore Create()
    {
        List<ScoreMaatra> sthayi = [];
        List<ScoreMaatra> antara = [];

        AddLine(sthayi, 1,
            "Sa;Dha. Ni.;Sa;Ga MaTivra;Pa.;Re Re;Ga;-",
            "रं;S;जिश;हीS;S;Sस;ही;S",
            (0, "Opening pickup. Starts on Dadra maatra 5."),
            (1, "Check lower-register marks and whether Dha is a kan swar."));
        AddLine(sthayi, 1,
            "*;*;Ga;MaTivra Ga;Ga;MaTivra Dha;Pa;MaTivra;MaTivra Re;Re;MaTivra;Dha",
            "*;*;दिल;हीS;दु;खाS;ने;S;के;लि;ए;S",
            (6, "Bracketed Pa (khatka). Meend from Pa toward Ma on the next maatra."),
            (8, "Check whether the stacked printed Ma–Re is a kan swar."));
        AddLine(sthayi, 2,
            "Pa;-;Ga Pa;Re Ga;Sa;Dha. Ni.",
            "आ;S;SS;SS;रं;S",
            (5, "Printed return cue to the sthayi. Check the stacked lower Dha and Ni."));
        AddLine(sthayi, 2,
            "?;?;?;-;Ni Sa';Dha Ni;Sa';Sa';Sa';Sa';Sa';-",
            "आ;S;S;S;आS;SS;फि;र;से;मु;झे;S",
            (0, "The first three melody cells on this printed line are blank. Resolve against the return cue before playback."));
        AddLine(sthayi, 2,
            "Ni;Re' Ni;Ni;Dha;MaTivra;Dha Pa;Pa;Re;Re;Re;Pa MaTivra;Dha Pa",
            "छो;SS;ड़;के;जा;SS;ने;S;के;लि;एS;SS",
            (6, "Meend from Pa to Re."));
        AddLine(sthayi, 2,
            "Pa;-;Ga Pa;Re Ga;Sa;Ni.",
            "आ;S;SS;SS;रं;S",
            (5, "Printed return cue to the sthayi."));

        AddLine(antara, 2,
            "Sa';Ni;Pa;Pa;MaTivra Ga;MaTivra;Ga Re;-",
            "कि;स;कि;स;कोS;ब;ताS;S",
            (0, "Antara pickup. Starts on Dadra maatra 5."),
            (2, "Printed Dha above Pa. Review kan before Pa."));
        AddLine(antara, 2,
            "Ga;-;Ga;Sa Sa;Ni.;Dha.;Ni.;Re;Re;Pa;Ga;Re Sa",
            "एं;S;गे;Sजु;दा;S;ई;S;का;स;ब;बS",
            (2, "Meend across the vibhaag line toward Sa."));
        AddLine(antara, 2,
            "Pa;-;Ga;Re Sa;Re MaTivra;MaTivra;MaTivra;MaTivra;MaTivra;MaTivra;MaTivra;Dha",
            "हम;S;S;SS;SS;किस;कि;स;को;ब;ता;S",
            (8, "Check the marks around the two Ma swaras on either side of the vibhaag line."),
            (11, "Printed Ni above Dha. Review kan before Dha."));
        AddLine(antara, 2,
            "Ga;?;Re;Re;Re;-;Re;Ni;Ni;Dha;Dha;Pa",
            "एं;S;गे;जु;दा;S;ई;S;का;स;ब;ब",
            (0, "Printed Ma above Ga. Review kan before Ga."),
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
            "Pa;-;Ga Pa;Re Ga;Sa;Dha. Ni.",
            "आ;S;SS;SS;रं;S",
            (5, "Printed return cue. Remaining verses are to follow the same pattern. Confirm arrangement before export."));

        return new CompositionScore("रंजिश ही सही", "Dadra",
            [new ScoreSection("Sthayi", 5, sthayi.AsReadOnly()), new ScoreSection("Antara", 5, antara.AsReadOnly())]);
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
            destination.Add(new ScoreMaatra(page, MaatraNotation.Parse(swaraCells[index]), lyricCells[index], text));
        }
    }
}
