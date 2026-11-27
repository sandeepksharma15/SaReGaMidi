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
    public void Render_RanjishKhatka_PlaysFourEqualScaleNotesAndGlidesFromFinalPa()
    {
        CompositionScore score = RanjishHiSahiDraft.Create();
        var cells = score.Sections.SelectMany(section => section.Maatras).ToList();
        int start = cells.FindIndex(cell => cell.Swaras.Any(note => note.Khatka && note.MeendToNext));
        var starts = new System.Collections.Generic.Dictionary<long, (int Maatra, int Swara)>();
        MidiFile file = new(new MemoryStream(MidiScoreExport.Render(score, new ScorePass("Turn", start, 2), 60, 90, starts)), true);
        var notes = file.Events[0].OfType<NoteOnEvent>().ToArray();
        Assert.Equal([67, 69, 66, 67, 66], notes.Select(note => note.NoteNumber));
        Assert.All(notes.Take(4), note =>
        {
            Assert.Equal(240, note.NoteLength);
            Assert.Equal((start, 0), starts[note.AbsoluteTime]);
        });
        Assert.Equal(notes[0].AbsoluteTime + 960, notes[4].AbsoluteTime);
        Assert.Equal(notes[3].AbsoluteTime, file.Events[0].OfType<PitchWheelChangeEvent>().First().AbsoluteTime);
    }

    [Theory]
    [InlineData(5, 64)]
    [InlineData(0, 61)]
    public void Render_Khatka_UsesAnalysisCutoffAndWrapsBelowSa(int cutoff, int upper)
    {
        ScoreMaatra turn = new(1, MaatraNotation.Parse("(Sa)"), "", "");
        ScoreMaatra common = new(1, MaatraNotation.Parse("Ga Ni"), "", "");
        ScoreMaatra rare = new(1, MaatraNotation.Parse("ReKomal"), "", "");
        CompositionScore score = new("Turn", "Dadra", [new ScoreSection("A", 1,
            [turn, .. Enumerable.Repeat(common, 12), rare])]);
        MidiFile file = new(new MemoryStream(MidiScoreExport.Render(score, new ScorePass("First beat", 0, 1),
            60, 120, rareCutoff: cutoff)), true);
        Assert.Equal([60, upper, 59, 60], file.Events[0].OfType<NoteOnEvent>().Select(note => note.NoteNumber));
    }

    [Fact]
    public void Render_ApniDhun_BendsOnlyKomalGaIntoSa()
    {
        ScoreMaatra phrase = new(1, MaatraNotation.Parse("ReKomal GaKomal~"), "", "");
        ScoreMaatra sa = new(1, MaatraNotation.Parse("Sa"), "", "");
        CompositionScore score = new("Apni Dhun", "Keharwa", [new ScoreSection("Sthayi", 7, [phrase, sa])]);
        ScorePass pass = new("Ending", 0, 2);
        MidiFile file = new(new MemoryStream(MidiScoreExport.Render(score, pass, 60, 120)), true);
        var melody = file.Events[0];
        long start = 6 * 960 + 480;
        long target = 7 * 960;
        var bends = melody.OfType<PitchWheelChangeEvent>().ToArray();
        Assert.Equal(start, bends[0].AbsoluteTime);
        Assert.Equal(8192, bends[0].Pitch);
        Assert.Equal(4096, bends.Single(b => b.AbsoluteTime == start + 240).Pitch);
        Assert.Equal(8192, bends[^1].Pitch);
        Assert.Equal(target, bends[^1].AbsoluteTime);
        Assert.Equal([61, 63, 60], melody.OfType<NoteOnEvent>().Select(n => n.NoteNumber));
        Assert.Equal([101, 100, 6, 38, 101, 100], melody.OfType<ControlChangeEvent>()
            .Where(e => e.AbsoluteTime == start).Select(e => (int)e.Controller));
        Assert.Equal([0, 0, 3, 0, 127, 127], melody.OfType<ControlChangeEvent>()
            .Where(e => e.AbsoluteTime == start).Select(e => e.ControllerValue));
        Assert.True(melody.IndexOf(bends[^1]) < melody.IndexOf(melody.OfType<NoteOnEvent>().Last()));
        CompositionScore plain = score with { Sections = [new ScoreSection("Sthayi", 7,
            [phrase with { Swaras = MaatraNotation.Parse("ReKomal GaKomal") }, sa])] };
        MidiFile control = new(new MemoryStream(MidiScoreExport.Render(plain, pass, 60, 120)), true);
        Assert.Equal(control.Events[1].Select(e => e.ToString()), file.Events[1].Select(e => e.ToString()));
    }

    [Fact]
    public void Render_AnchoredMeend_HoldsThroughIntermediatePitchAndClipsAtPassageBoundary()
    {
        var anchor = System.Guid.NewGuid();
        ScoreMaatra[] cells = new[] { "Sa", "Ga", "-", "Pa", "Re" }
            .Select(text => new ScoreMaatra(1, MaatraNotation.Parse(text), "", "")).ToArray();
        cells[0] = cells[0] with { Swaras = [cells[0].Swaras[0] with { MeendTargetId = anchor }] };
        cells[3] = cells[3] with { Swaras = [cells[3].Swaras[0] with { AnchorId = anchor }] };
        CompositionScore score = new("Glide", "Dadra", [new ScoreSection("A", 1, cells)]);
        MidiFile file = new(new MemoryStream(MidiScoreExport.Render(score, new ScorePass("All", 0, 5), 60, 90)), true);
        var notes = file.Events[0].OfType<NoteOnEvent>().ToArray();
        Assert.Equal([60, 67, 62], notes.Select(note => note.NoteNumber));
        Assert.Equal(2880, notes[0].NoteLength);
        Assert.Equal(2880, notes[1].AbsoluteTime);
        Assert.Equal(12873, file.Events[0].OfType<PitchWheelChangeEvent>().Single(e => e.AbsoluteTime == 960).Pitch);
        MidiFile clipped = new(new MemoryStream(MidiScoreExport.Render(score, new ScorePass("Start", 0, 2), 60, 90)), true);
        Assert.Empty(clipped.Events[0].OfType<PitchWheelChangeEvent>());
    }

    [Fact]
    public void Render_DadraPickup_ReadsBackFourAlignedTracks()
    {
        CompositionScore score = RanjishHiSahiDraft.Create();
        byte[] bytes = MidiScoreExport.Render(score, score.Passes[0], 62, 120);
        MidiFile file = new(new MemoryStream(bytes), true);

        Assert.Equal(1, file.FileFormat);
        Assert.Equal(4, file.Tracks);
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
        Assert.Equal(5 * 960, notes[1].AbsoluteTime);
        Assert.Equal(59, notes[1].NoteNumber); // Mandra Dha with D4 as Sa.
        Assert.Equal(120, notes[1].NoteLength);
        Assert.Equal(61, notes[2].NoteNumber); // Mandra Ni fills the rest of the maatra.
        Assert.Equal(5 * 960 + 120, notes[2].AbsoluteTime);
        Assert.Equal(840, notes[2].NoteLength);
        Assert.True(notes[1].Velocity < notes[2].Velocity);
        Assert.Equal(6 * 960, notes[3].AbsoluteTime);
        Assert.Equal(480, notes[4].NoteLength);
        Assert.Equal(1920, notes.Single(note => note.AbsoluteTime == 10 * 960).NoteLength);
        NoteOnEvent[] rhythm = theka.OfType<NoteOnEvent>().ToArray();
        Assert.All(rhythm, note => Assert.Equal(10, note.Channel));
        Assert.Equal(notes[3].AbsoluteTime, rhythm[2].AbsoluteTime);
        Assert.Equal(36, rhythm[2].NoteNumber);
        Assert.Equal(melody.Last().AbsoluteTime, theka.Last().AbsoluteTime);
    }

    [Fact]
    public void Render_KanOnSubdivision_HighlightsPrincipalAndStartsMeendAfterGrace()
    {
        ScoreMaatra phrase = new(1, MaatraNotation.Parse("Sa Dha>Ni~"), "", "");
        ScoreMaatra hold = new(1, MaatraNotation.Parse("-"), "", "");
        ScoreMaatra destination = new(1, MaatraNotation.Parse("Sa'"), "", "");
        CompositionScore score = new("Grace and glide", "Dadra", [new ScoreSection("A", 1, [phrase, hold, destination])]);
        System.Collections.Generic.Dictionary<long, (int Maatra, int Swara)> starts = [];
        MidiFile file = new(new MemoryStream(MidiScoreExport.Render(score, new ScorePass("All", 0, 3), 60, 120, starts)), true);
        NoteOnEvent[] notes = file.Events[0].OfType<NoteOnEvent>().ToArray();
        Assert.Equal([60, 69, 71, 72], notes.Select(n => n.NoteNumber));
        Assert.Equal([0L, 480L, 540L, 1920L], notes.Select(n => n.AbsoluteTime));
        Assert.Equal(60, notes[1].NoteLength);
        Assert.Equal(1380, notes[2].NoteLength);
        Assert.Equal((0, 1), starts[480]);
        Assert.Equal((0, 1), starts[540]);
        Assert.Equal(540, file.Events[0].OfType<PitchWheelChangeEvent>().First().AbsoluteTime);
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
    public void Render_Selection_StartsImmediatelyOnSelectedTaalBeat()
    {
        var score = RanjishHiSahiDraft.Create();
        var cells = score.Sections.SelectMany(section => section.Maatras).ToList();
        int index = cells.FindIndex(cell => cell.Swaras.Any(note => note.Khatka && note.MeendToNext));
        var passage = new ScorePass("Selection", index, 2);
        MidiFile midi = new(new MemoryStream(MidiScoreExport.Render(score, passage, 60, 90, immediateStart: true)), true);
        Assert.Equal([0L, 240L, 480L, 720L, 960L], midi.Events[0].OfType<NoteOnEvent>().Select(n => n.AbsoluteTime));
        Assert.Equal(1920, midi.Events[1].Last().AbsoluteTime);
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(1, 6)]
    public void Render_SectionPickup_FillsGapOrAddsCycleOnDrums(int entryBeat, int bridge)
    {
        ScoreMaatra sa = new(1, MaatraNotation.Parse("Sa"), "", "");
        CompositionScore score = new("Bridge", "Dadra", [new ScoreSection("Sthayi", 1, Enumerable.Repeat(sa, 6).ToArray()),
            new ScoreSection("Antara", entryBeat, [sa])]);
        MidiFile midi = new(new MemoryStream(MidiScoreExport.Render(score, new ScorePass("All", 0, 7), 60, 90)), true);
        Assert.Equal((6 + bridge) * 960, midi.Events[0].OfType<NoteOnEvent>().Last().AbsoluteTime);
        Assert.Equal(Enumerable.Range(6, bridge).Select(i => i * 960L),
            midi.Events[1].OfType<NoteOnEvent>().Where(n => n.AbsoluteTime >= 5760 && n.AbsoluteTime < (6 + bridge) * 960)
                .Select(n => n.AbsoluteTime));
        Assert.DoesNotContain(midi.Events[2].OfType<NoteOnEvent>(), n => n.AbsoluteTime >= 5760 && n.AbsoluteTime < (6 + bridge) * 960);
        Assert.DoesNotContain(midi.Events[3].OfType<NoteOnEvent>(), n => n.AbsoluteTime >= 5760 && n.AbsoluteTime < (6 + bridge) * 960);
    }

    [Fact]
    public void Render_Keharwa_UsesUnarticulatedAntaraPickupForDrums()
    {
        ScoreMaatra ma = new(1, MaatraNotation.Parse("Ma"), "", "");
        ScoreMaatra hold = new(1, MaatraNotation.Parse("-"), "", "");
        CompositionScore score = new("Apni Dhun", "Keharwa", [new ScoreSection("स्थायी", 1, Enumerable.Repeat(ma, 8).ToArray()),
            new ScoreSection("अंतरा", 1, [hold, ma])]);
        MidiFile midi = new(new MemoryStream(MidiScoreExport.Render(score, new ScorePass("All", 0, 10), 60, 90)), true);
        var notes = midi.Events[0].OfType<NoteOnEvent>().ToArray();
        Assert.Equal(960, notes[7].NoteLength);
        Assert.Equal(9 * 960, notes[8].AbsoluteTime);
        Assert.Contains(midi.Events[1].OfType<NoteOnEvent>(), hit => hit.AbsoluteTime == 8 * 960);
    }

    [Fact]
    public void Render_Chords_UsesFirstAttackInDivisionAndOffersSmoothAlternative()
    {
        var cells = new[] { "*", "Sa", "Ga", "Ga", "Pa", "Ni" }.Select(s => new ScoreMaatra(1, MaatraNotation.Parse(s), "", "")).ToArray();
        CompositionScore score = new("Chords", "Dadra", [new ScoreSection("A", 1, cells)]);
        foreach (bool smooth in new[] { false, true })
        {
            MidiFile midi = new(new MemoryStream(MidiScoreExport.Render(score, new ScorePass("All", 0, 6), 60, 90,
                smoothChords: smooth)), true);
            var chords = midi.Events[2].OfType<NoteOnEvent>().ToArray();
            var timeline = ScorePerformance.BuildTimeline(score, [new ScorePass("All", 0, 6)]);
            var labels = MidiScoreExport.PlanHarmony(TaalCatalog.FindByName("Dadra")!, timeline,
                MidiScoreExport.FrequentOffsets(score, 5), 60, smooth).ToArray();
            Assert.Equal(["C", smooth ? "C" : "Em"], labels.Select(label => label.Name(60)));
            foreach (var label in labels)
                Assert.Equal(label.Pitches, chords.Where(note => note.AbsoluteTime == label.Start * 960)
                    .Select(note => note.NoteNumber));
            Assert.Equal("Chords", midi.Events[2].OfType<TextEvent>().First().Text);
            Assert.Equal([48, 52, 55], chords.Where(n => n.AbsoluteTime == 0).Select(n => n.NoteNumber));
            Assert.Equal(smooth ? [48, 52, 55] : new[] { 52, 55, 59 }, chords.Where(n => n.AbsoluteTime == 2880).Select(n => n.NoteNumber));
            Assert.All(chords, n => { Assert.Equal(2, n.Channel); Assert.Equal(2880, n.NoteLength); });
            var bass = midi.Events[3].OfType<NoteOnEvent>().ToArray();
            Assert.Equal("Bass", midi.Events[3].OfType<TextEvent>().First().Text);
            Assert.Equal(32, Assert.Single(midi.Events[3].OfType<PatchChangeEvent>()).Patch);
            Assert.Equal([36, 43, smooth ? 36 : 28], bass.Select(n => n.NoteNumber));
            Assert.Equal([0L, 1920L, 2880L], bass.Select(n => n.AbsoluteTime));
            Assert.Equal([1920, 960, 2880], bass.Select(n => n.NoteLength));
            Assert.All(bass, n => Assert.Equal(3, n.Channel));
            Assert.True(bass[2].Velocity < bass[0].Velocity);
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
