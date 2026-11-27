using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using NAudio.Midi;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

public partial class TranscriptionWindow
{
    private CancellationTokenSource? _playbackCancellation;
    private MidiAudition? _audition;
    private ScorePass? _lastPassage;
    private int _sa = 60;
    private int _tempo = 90;
    private bool _loop;
    private bool _smoothChords;

    private MidiExportWindow? ChooseMidiSettings(CompositionScore score, bool playback)
    {
        MidiExportWindow options = new(score, _lastPassage, _sa, _tempo, playback, playback ? SelectedPhrase() : null,
            _loop, _smoothChords) { Owner = this };
        if (options.ShowDialog() != true)
            return null;

        _lastPassage = options.Passage;
        _sa = options.Sa;
        _tempo = options.Tempo;
        _smoothChords = options.SmoothChords;
        if (playback)
            _loop = options.Loop;
        return options;
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_playbackCancellation is not null)
        {
            _playbackCancellation.Cancel();
            return;
        }
        if (_busy || !_canEdit || !CommitGrid())
            return;

        try
        {
            CompositionScore score = BuildScore();
            MidiExportWindow? options = ChooseMidiSettings(score, playback: true);
            if (options is null)
                return;
            Dictionary<long, (int Maatra, int Swara)> starts = [];
            byte[] midi = MidiScoreExport.Render(score, options.Passage, options.Sa, options.Tempo, starts,
                (int)RareCutoff.Value, immediateStart: options.Passage == SelectedPhrase(), smoothChords: _smoothChords);
            if (MidiOut.NumberOfDevices == 0)
                throw new InvalidOperationException("Windows has no MIDI output device available.");

            using MidiOut output = new(-1); // MIDI_MAPPER selects the default Windows output.
            using CancellationTokenSource cancellation = new();
            _playbackCancellation = cancellation;
            _audition = new MidiAudition(output.Send);
            try
            {
                ApplyTrackVolumes();
                ScoreTabs.SelectedIndex = 0;
                PlayButton.Content = "■  _Stop";
                StatusText.Text = $"Playing {options.Passage.Name} · {options.Tempo} maatras per minute";
                UpdateActions();
                do
                {
                    await _audition.PlayAsync(midi, cancellation.Token, tick =>
                        HighlightPlayingSwara(starts.TryGetValue(tick, out var position) ? position : null), starts.ContainsKey);
                    if (_loop)
                    {
                        output.Reset();
                        output.Send(new PitchWheelChangeEvent(0, 1, 8192).GetAsShortMessage());
                        ApplyTrackVolumes();
                        HighlightPlayingSwara(null);
                    }
                } while (_loop && !cancellation.IsCancellationRequested);
                StatusText.Text = "Playback finished.";
            }
            finally
            {
                HighlightPlayingSwara(null);
                _audition = null;
                _playbackCancellation = null;
                output.Reset();
                // Stop may interrupt a bend before its reset event is reached.
                output.Send(new PitchWheelChangeEvent(0, 1, 8192).GetAsShortMessage());
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Playback stopped.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException
            or FormatException or OverflowException or InvalidOperationException or NAudio.MmException)
        {
            StatusText.Text = $"Could not play the passage. {exception.Message}";
        }
        finally
        {
            PlayButton.Content = "▶  _Play";
            UpdateActions();
        }
    }

    private void Track_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyTrackVolumes();
        }
        catch (NAudio.MmException)
        {
            _playbackCancellation?.Cancel();
        }
    }

    private void ApplyTrackVolumes()
    {
        _audition?.SetAudible(1, MelodyBox.IsChecked == true);
        _audition?.SetAudible(10, ThekaBox.IsChecked == true);
        _audition?.SetAudible(2, ChordsBox.IsChecked == true);
    }

    protected override void OnClosed(EventArgs e)
    {
        _playbackCancellation?.Cancel();
        base.OnClosed(e);
    }
}
