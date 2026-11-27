using System.Collections.Generic;
using System.Linq;

namespace SaReGaMidi.Music.Score;

public static class ScorePerformance
{
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
