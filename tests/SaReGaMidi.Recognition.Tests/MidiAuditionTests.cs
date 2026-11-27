using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Midi;
using SaReGaMidi.Music.Score;
using SaReGaMidi.UI;
using Xunit;

namespace SaReGaMidi.Recognition.Tests;

public sealed class MidiAuditionTests
{
    [Fact]
    public async Task PlayAsync_ReferencePickup_SendsBothExportedTracksInOrder()
    {
        CompositionScore score = RanjishHiSahiDraft.Create();
        byte[] midi = MidiScoreExport.Render(score, new ScorePass("Pickup to sam", 0, 3), 60, 400);
        MidiFile file = new(new MemoryStream(midi), true);
        int[] expected = file.Events[0].Concat(file.Events[1]).OfType<NoteEvent>()
            .OrderBy(note => note.AbsoluteTime).Select(note => note.GetAsShortMessage()).ToArray();
        List<int> sent = [];
        MidiAudition audition = new(sent.Add);

        await audition.PlayAsync(midi, CancellationToken.None);

        Assert.Equal(expected, sent);
    }

    [Fact]
    public void SetAudible_ChangesOnlyTheSelectedChannelVolume()
    {
        List<int> messages = [];
        MidiAudition audition = new(messages.Add);
        audition.SetAudible(1, false);
        audition.SetAudible(10, false);
        audition.SetAudible(1, true);
        audition.SetAudible(10, true);

        Assert.Equal([0x0007B0, 0x0007B9, 0x6407B0, 0x6407B9], messages);
    }

    [Fact]
    public async Task PlayAsync_StopDuringPickup_SendsNoLaterNotes()
    {
        CompositionScore score = RanjishHiSahiDraft.Create();
        byte[] midi = MidiScoreExport.Render(score, score.Passes[0], 60, 90);
        List<int> sent = [];
        using CancellationTokenSource cancellation = new();
        Task playback = new MidiAudition(sent.Add).PlayAsync(midi, cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(() => playback);
        Assert.Empty(sent);
    }
}
