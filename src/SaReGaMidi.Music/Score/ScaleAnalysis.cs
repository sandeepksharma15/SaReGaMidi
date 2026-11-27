using System;
using System.Collections.Generic;
using System.Linq;

namespace SaReGaMidi.Music.Score;

public sealed record SwaraUsage(Swara Swara, int Count, int KanCount)
{
    public bool IsFrequent(int totalAttacks, int cutoffPercent)
        => Count > 0 && Count * 100L >= (long)totalAttacks * cutoffPercent;
}

public static class ScaleAnalysis
{
    private static readonly (string Name, Swara[] Notes)[] _scales =
    [
        ("major", [Swara.Sa, Swara.Re, Swara.Ga, Swara.Ma, Swara.Pa, Swara.Dha, Swara.Ni]),
        ("natural minor", [Swara.Sa, Swara.Re, Swara.GaKomal, Swara.Ma, Swara.Pa, Swara.DhaKomal, Swara.NiKomal]),
        ("Dorian", [Swara.Sa, Swara.Re, Swara.GaKomal, Swara.Ma, Swara.Pa, Swara.Dha, Swara.NiKomal]),
        ("Phrygian", [Swara.Sa, Swara.ReKomal, Swara.GaKomal, Swara.Ma, Swara.Pa, Swara.DhaKomal, Swara.NiKomal]),
        ("Lydian", [Swara.Sa, Swara.Re, Swara.Ga, Swara.MaTivra, Swara.Pa, Swara.Dha, Swara.Ni]),
        ("Mixolydian", [Swara.Sa, Swara.Re, Swara.Ga, Swara.Ma, Swara.Pa, Swara.Dha, Swara.NiKomal]),
        ("Locrian", [Swara.Sa, Swara.ReKomal, Swara.GaKomal, Swara.Ma, Swara.MaTivra, Swara.DhaKomal, Swara.NiKomal]),
        ("harmonic minor", [Swara.Sa, Swara.Re, Swara.GaKomal, Swara.Ma, Swara.Pa, Swara.DhaKomal, Swara.Ni]),
        ("melodic minor (ascending)", [Swara.Sa, Swara.Re, Swara.GaKomal, Swara.Ma, Swara.Pa, Swara.Dha, Swara.Ni]),
        ("major pentatonic", [Swara.Sa, Swara.Re, Swara.Ga, Swara.Pa, Swara.Dha]),
        ("minor pentatonic", [Swara.Sa, Swara.GaKomal, Swara.Ma, Swara.Pa, Swara.NiKomal])
    ];

    public static IReadOnlyList<SwaraUsage> CountNotes(IEnumerable<SwaraEvent> events)
    {
        int[] main = new int[12];
        int[] kan = new int[12];
        foreach (SwaraEvent note in events)
        {
            if (note.Kind != SwaraEventKind.Note || note.Pitch is not SwaraPitch pitch)
                continue;
            main[(int)pitch.Swara]++;
            if (note.KanSwara is SwaraPitch grace)
                kan[(int)grace.Swara]++;
        }
        return Enum.GetValues<Swara>().Select(swara => new SwaraUsage(swara, main[(int)swara], kan[(int)swara]))
            .Where(usage => usage.Count + usage.KanCount > 0).ToArray();
    }

    public static string? MatchWesternScale(IEnumerable<Swara> swaras)
    {
        HashSet<Swara> notes = swaras.ToHashSet();
        return _scales.FirstOrDefault(scale => notes.SetEquals(scale.Notes)).Name;
    }
}
