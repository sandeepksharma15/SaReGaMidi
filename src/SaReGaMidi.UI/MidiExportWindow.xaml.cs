using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

public partial class MidiExportWindow : Window
{
    internal MidiExportWindow(CompositionScore score, ScorePass? previousPassage, int sa, int tempo, bool playback,
        ScorePass? selection, bool loop, bool smoothChords)
    {
        InitializeComponent();
        HarmonyBox.SelectedIndex = smoothChords ? 1 : 0;
        if (playback)
        {
            Title = "Play transcription";
            ContinueButton.Content = "_Play";
            LoopBox.Visibility = Visibility.Visible;
            LoopBox.IsChecked = loop;
        }
        List<ScorePass> passages = [new("Whole written score", 0, score.Sections.Sum(section => section.Maatras.Count))];
        passages.AddRange(score.Passes);
        if (selection is not null)
            passages.Add(selection);
        int start = 0;
        foreach (ScoreSection section in score.Sections)
        {
            if (score.Sections.Count > 1)
                passages.Add(new ScorePass($"{section.Name} (written)", start, section.Maatras.Count));
            start += section.Maatras.Count;
        }
        PassageBox.ItemsSource = passages;
        PassageBox.SelectedIndex = score.Passes.Count == 0 ? 0 : 1;
        if (previousPassage is not null && passages.Contains(previousPassage))
            PassageBox.SelectedItem = previousPassage;
        if (selection is not null)
            PassageBox.SelectedItem = selection;
        TempoBox.Text = tempo.ToString();
        string[] names = ["C", "C♯", "D", "E♭", "E", "F", "F♯", "G", "A♭", "A", "B♭", "B"];
        SaBox.ItemsSource = Enumerable.Range(48, 36).Select(note => $"{names[note % 12]}{note / 12 - 1} · MIDI {note}").ToArray();
        SaBox.SelectedIndex = sa - 48;
    }

    internal ScorePass Passage => (ScorePass)PassageBox.SelectedItem;
    internal bool Loop => LoopBox.IsChecked == true;
    internal bool SmoothChords => HarmonyBox.SelectedIndex == 1;
    internal int Sa => SaBox.SelectedIndex + 48;
    internal int Tempo { get; private set; }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TempoBox.Text, out int tempo) || tempo is < 20 or > 400)
        {
            MessageBox.Show(this, "Enter a tempo from 20 to 400 maatras per minute.", Title);
            TempoBox.Focus();
            TempoBox.SelectAll();
            return;
        }
        Tempo = tempo;
        DialogResult = true;
    }
}
