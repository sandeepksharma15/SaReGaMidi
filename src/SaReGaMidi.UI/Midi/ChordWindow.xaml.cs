using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NAudio.Midi;
using SaReGaMidi.Music;
using SaReGaMidi.Music.Score;
using static SaReGaMidi.UI.MidiScoreExport;

namespace SaReGaMidi.UI;

public partial class ChordWindow : Window
{
    private readonly int _sa;
    private readonly Func<ChordChoice, byte[]> _render;
    private readonly Func<ChordChoice?, Task> _save;
    private readonly List<HarmonyDivision> _choices = [];
    private CancellationTokenSource? _playing;
    private bool _saving;

    internal ChordWindow(HarmonyDivision current, int sa, bool hasOverride, Func<ChordChoice, byte[]> render,
        Func<ChordChoice?, Task> save)
    {
        InitializeComponent();
        _sa = sa;
        _render = render;
        _save = save;
        PrincipalText.Text = $"First principal swara · {DescribePitch(PitchConverter.GetMidiNote(current.Principal, sa))}";
        RestoreButton.IsEnabled = hasOverride;
        Add(current, hasOverride ? "Saved choice" : "Automatic");
        foreach (HarmonyDivision suggestion in current.Suggestions)
        {
            if (suggestion.Root == current.Root && suggestion.Pitches.SequenceEqual(current.Pitches))
                continue;
            Add(suggestion, suggestion == current.Suggestions[0] ? "Automatic" : "Alternative");
        }
        Choices.SelectedIndex = 0;

        void Add(HarmonyDivision choice, string label)
        {
            _choices.Add(choice);
            Choices.Items.Add($"{choice.Name(sa)} · {label}");
        }
    }

    private string DescribePitch(int midi)
    {
        int degree = (midi - _sa + 120) % 12;
        return $"{(Swara)degree} ({WesternNoteNames.Format(midi - degree, (Swara)degree)})";
    }

    private void Choice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Choices.SelectedIndex < 0)
            return;
        HarmonyDivision choice = _choices[Choices.SelectedIndex];
        ToneText.Text = $"Chord tones · {string.Join(", ", choice.Pitches.Select(DescribePitch))}";
        BassText.Text = $"Bass · {DescribePitch(choice.BassRoot)}";
        if (choice.PlayFifth)
            BassText.Text += $" → {DescribePitch(choice.BassRoot + 7)} on the last beat";
    }

    private async void Listen_Click(object sender, RoutedEventArgs e)
    {
        if (_playing is not null)
        {
            _playing.Cancel();
            return;
        }
        using CancellationTokenSource cancellation = new();
        _playing = cancellation;
        Choices.IsEnabled = false;
        ListenButton.Content = "_Stop";
        try
        {
            byte[] midi = _render(_choices[Choices.SelectedIndex].RelativeTo(_sa));
            using MidiOut output = new(-1);
            try
            {
                MidiAudition audition = new(output.Send);
                foreach (int channel in new[] { 1, 10, 2, 3 })
                    audition.SetAudible(channel, true);
                await audition.PlayAsync(midi, cancellation.Token);
            }
            finally
            {
                output.Reset();
                output.Send(new PitchWheelChangeEvent(0, 1, 8192).GetAsShortMessage());
            }
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Audition stopped.";
        }
        catch (Exception error) when (error is NAudio.MmException or IOException or InvalidDataException or ArgumentException
            or FormatException or OverflowException or InvalidOperationException)
        {
            Status.Text = $"Could not play this vibhaag. {error.Message}";
        }
        finally
        {
            _playing = null;
            Choices.IsEnabled = true;
            ListenButton.Content = "_Listen";
        }
    }

    private async void Use_Click(object sender, RoutedEventArgs e) => await SaveAsync(_choices[Choices.SelectedIndex].RelativeTo(_sa));

    private async void Restore_Click(object sender, RoutedEventArgs e) => await SaveAsync(null);

    private async Task SaveAsync(ChordChoice? choice)
    {
        _playing?.Cancel();
        _saving = true;
        IsEnabled = false;
        try
        {
            await _save(choice);
            _saving = false;
            DialogResult = true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            Status.Text = $"Could not save the score. {error.Message}";
        }
        finally
        {
            _saving = false;
            IsEnabled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        e.Cancel |= _saving;
        _playing?.Cancel();
    }
}
