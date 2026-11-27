using System.Linq;
using SaReGaMidi.Music.Score;
using SaReGaMidi.UI;
using Xunit;

namespace SaReGaMidi.Recognition.Tests;

public sealed class ScorePerformanceTests
{
    [Fact]
    public void BuildTimeline_ReferenceSthayi_ResolvesPickupAndAlternateRow()
    {
        CompositionScore score = RanjishHiSahiDraft.Create();
        PlaybackTimeline opening = ScorePerformance.BuildTimeline(score, [score.Passes[0]]);
        PlaybackTimeline alternate = ScorePerformance.BuildTimeline(score, [score.Passes[1]]);

        Assert.Equal([5, 6, 1], opening.Beats.Take(3).Select(beat => beat.TaalBeat));
        Assert.Equal(new PlaybackTime(2, 1), opening.Notes[2].Start);
        Assert.Equal([1, 2, 3, 4, 5, 6, 1], alternate.Beats.Take(7).Select(beat => beat.TaalBeat));
        Assert.Equal([20, 21, 22, 29, 30, 31, 32], alternate.Beats.Take(7).Select(beat => beat.SwaraSourceIndex));
        Assert.Equal("Pa", MaatraNotation.Format([alternate.Notes[0].Swara]));
        Assert.Equal(new PlaybackTime(2, 1), alternate.Notes[0].End);
        Assert.Equal(new PlaybackTime(5, 2), alternate.Notes[2].Start);
        Assert.Equal(new PlaybackTime(4, 1), alternate.Notes[2].End);
        Assert.Equal("Ni", alternate.Notes[3].Swara.Pitch!.Value.Swara.ToString());
        Assert.Equal(new PlaybackTime(4, 1), alternate.Notes[3].Start);
    }

    [Fact]
    public void BuildTimeline_ContinuationAndRest_KeepExactTimingAcrossRanges()
    {
        ScoreMaatra[] cells = new[] { "Sa:1/3 Ga:2/3", "S", "-", "*", "-", "?", "Re" }
            .Select(text => new ScoreMaatra(1, MaatraNotation.Parse(text), "S", "")).ToArray();
        CompositionScore score = new("Test", "Dadra", [new ScoreSection("A", 1, cells)]);

        PlaybackTimeline timeline = ScorePerformance.BuildTimeline(score,
            [new ScorePass("Start", 0, 1), new ScorePass("Continue", 1, 6)]);

        Assert.Equal(3, timeline.Notes.Count);
        Assert.Equal(new PlaybackTime(1, 3), timeline.Notes[1].Start);
        Assert.Equal(new PlaybackTime(3, 1), timeline.Notes[1].End);
        Assert.Equal(new PlaybackTime(6, 1), timeline.Notes[2].Start);
        Assert.Equal(5, Assert.Single(timeline.UnclearBeats).WrittenIndex);
    }
}
