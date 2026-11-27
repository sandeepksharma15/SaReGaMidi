using System.Linq;
using SaReGaMidi.Music;
using SaReGaMidi.Music.Score;
using Xunit;

namespace SaReGaMidi.Recognition.Tests;

public sealed class ScaleAnalysisTests
{
    [Fact]
    public void CountsPrincipalNotesAcrossOctavesAndKeepsKanSeparate()
    {
        var notes = new[] { "Sa", "Sa'", "Dha>Ni", "-", "*", "?" }.SelectMany(text => MaatraNotation.Parse(text));
        Assert.Equal([new(Swara.Sa, 2, 0), new(Swara.Dha, 0, 1), new(Swara.Ni, 1, 0)], ScaleAnalysis.CountNotes(notes));
        Assert.True(new SwaraUsage(Swara.Re, 1, 0).IsFrequent(20, 5));
        Assert.False(new SwaraUsage(Swara.Re, 1, 0).IsFrequent(21, 5));
        Assert.False(new SwaraUsage(Swara.Re, 0, 1).IsFrequent(20, 0));
    }

    [Fact]
    public void MatchesExactIntervalsAndSpellsNotesRelativeToSa()
    {
        Swara[] major = [Swara.Sa, Swara.Re, Swara.Ga, Swara.Ma, Swara.Pa, Swara.Dha, Swara.Ni];
        Assert.Equal("major", ScaleAnalysis.MatchWesternScale(major));
        Assert.Null(ScaleAnalysis.MatchWesternScale(major.Skip(1)));
        Assert.Equal(["C♯4", "D♯4", "E♯4", "F♯4", "G♯4", "A♯4", "B♯4"],
            major.Select(swara => WesternNoteNames.Format(61, swara)));
        Assert.Equal("C5", WesternNoteNames.Format(65, Swara.Pa));
    }
}
