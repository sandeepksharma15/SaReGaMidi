using System.IO;
using System.Linq;
using NAudio.Midi;
using SaReGaMidi.Music.Rhythm;
using SaReGaMidi.Music.Score;
using SaReGaMidi.UI;
using Xunit;

namespace SaReGaMidi.Recognition.Tests;

public sealed class MidiScoreExportTests
{
    [Fact]
    public void Render_DadraPickup_ReadsBackTwoAlignedTracks()
    {
        CompositionScore score = RanjishHiSahiDraft.Create();
        byte[] bytes = MidiScoreExport.Render(score, score.Passes[0], 62, 120);
        MidiFile file = new(new MemoryStream(bytes), true);

        Assert.Equal(1, file.FileFormat);
        Assert.Equal(2, file.Tracks);
        Assert.Equal(960, file.DeltaTicksPerQuarterNote);
        var melody = file.Events[0];
        var theka = file.Events[1];
        Assert.Equal("Melody", melody.OfType<TextEvent>().First().Text);
        Assert.Equal("Theka - Dadra", theka.OfType<TextEvent>().First().Text);
        Assert.Equal(500000, Assert.Single(melody.OfType<TempoEvent>()).MicrosecondsPerQuarterNote);
        Assert.Equal(6, Assert.Single(melody.OfType<TimeSignatureEvent>()).Numerator);
        NoteOnEvent[] notes = melody.OfType<NoteOnEvent>().ToArray();
        Assert.Equal(62, notes[0].NoteNumber);
        Assert.Equal(4 * 960, notes[0].AbsoluteTime);
        Assert.Equal(6 * 960, notes[2].AbsoluteTime);
        Assert.Equal(480, notes[3].NoteLength);
        Assert.Equal(1920, notes.Single(note => note.AbsoluteTime == 10 * 960).NoteLength);
        NoteOnEvent[] rhythm = theka.OfType<NoteOnEvent>().ToArray();
        Assert.All(rhythm, note => Assert.Equal(10, note.Channel));
        Assert.Equal(notes[2].AbsoluteTime, rhythm[2].AbsoluteTime);
        Assert.Equal(36, rhythm[2].NoteNumber);
        Assert.Equal(melody.Last().AbsoluteTime, theka.Last().AbsoluteTime);
    }

    [Fact]
    public void Render_EachCatalogTheka_WritesOneCycleIncludingRestsAndSubdivisions()
    {
        foreach (Taal taal in TaalCatalog.All)
        {
            ScoreMaatra cell = new(1, MaatraNotation.Parse("Sa"), "", "");
            CompositionScore score = new("Cycle", taal.Name,
                [new ScoreSection("A", 1, Enumerable.Repeat(cell, taal.MaatraCount).ToArray())]);
            byte[] bytes = MidiScoreExport.Render(score, new ScorePass("Cycle", 0, taal.MaatraCount), 60, 90);
            MidiFile file = new(new MemoryStream(bytes), true);
            long[] hits = file.Events[1].OfType<NoteOnEvent>().Select(note => note.AbsoluteTime).ToArray();
            string?[] bols = taal.Vibhaags.SelectMany(division => division.Theka).ToArray();
            for (int beat = 0; beat < bols.Length; beat++)
            {
                Assert.Equal(bols[beat] is not null, hits.Contains(beat * 960L));
                if (bols[beat] == "तिरकिट")
                    Assert.Equal(4, hits.Count(tick => tick / 960 == beat));
            }
        }
    }

    [Fact]
    public void Render_UnclearBeat_ReportsItsLocation()
    {
        CompositionScore score = new("Draft", "Dadra",
            [new ScoreSection("Antara", 5, [new ScoreMaatra(1, MaatraNotation.Parse("?"), "", "")])]);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            MidiScoreExport.Render(score, new ScorePass("A", 0, 1), 60, 90));
        Assert.Contains("Antara, beat 5 (written maatra 1)", error.Message);
    }
}
