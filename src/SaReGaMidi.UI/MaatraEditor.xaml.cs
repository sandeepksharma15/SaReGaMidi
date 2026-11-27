using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SaReGaMidi.Music;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

public partial class MaatraEditor : Window
{
    #region Fields

    private readonly TranscriptionRow _row;
    private readonly int _cycleLength;
    private readonly ObservableCollection<EventEntry> _events = [];
    private EventEntry? _kanTarget;

    #endregion

    #region Constructors

    internal MaatraEditor(TranscriptionRow row, int pageCount, int cycleLength)
    {
        _row = row;
        _cycleLength = cycleLength;
        InitializeComponent();
        SectionBox.Text = row.Section;
        StartBeatBox.Text = row.StartBeat?.ToString() ?? string.Empty;
        PageBox.ItemsSource = Enumerable.Range(1, pageCount);
        PageBox.SelectedItem = row.SourcePage;
        LyricsBox.Text = row.Lyrics;
        NotesBox.Text = row.Annotation;
        GroupBox.IsChecked = row.ShowGrouping;
        EventsList.ItemsSource = _events;
        SaptakBox.ItemsSource = Enum.GetValues<Saptak>();
        SaptakBox.SelectedItem = Saptak.Madhya;

        if (string.IsNullOrWhiteSpace(row.Swaras))
            return;

        try
        {
            foreach (string token in MaatraNotation.Format(MaatraNotation.Parse(row.Swaras)).Split(' '))
            {
                string[] fields = token.Split(':');
                SwaraEvent note = MaatraNotation.Parse(fields[0])[0];
                _events.Add(new EventEntry(note, fields.Length == 2 ? fields[1] : string.Empty));
            }
        }
        catch (FormatException)
        {
            Message.Text = $"Check the earlier entry: {row.Swaras}";
        }

        UpdatePreview();
    }

    #endregion

    #region Methods

    private void Saptak_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SaptakBox.SelectedItem is not Saptak saptak)
            return;

        Palette.Children.Clear();
        foreach (Swara swara in Enum.GetValues<Swara>())
        {
            SwaraPitch pitch = new(swara, saptak);
            SwaraEvent note = new(SwaraEventKind.Note, pitch, new MaatraFraction(1, 1));
            Button button = new()
            {
                Content = SwaraGlyph.Create(pitch),
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 4, 5),
                ToolTip = $"{swara} · {saptak}"
            };
            AutomationProperties.SetName(button, $"{swara}, {saptak}");
            button.Click += (_, _) => SelectSwara(note);
            Palette.Children.Add(button);
        }
    }

    private void Sustain_Click(object sender, RoutedEventArgs e)
        => AddEvent(new SwaraEvent(SwaraEventKind.Sustain, null, new MaatraFraction(1, 1)));

    private void Silence_Click(object sender, RoutedEventArgs e)
        => AddEvent(new SwaraEvent(SwaraEventKind.Silence, null, new MaatraFraction(1, 1)));

    private void Unclear_Click(object sender, RoutedEventArgs e)
        => AddEvent(new SwaraEvent(SwaraEventKind.Unclear, null, new MaatraFraction(1, 1)));

    private void SelectSwara(SwaraEvent note)
    {
        if (_kanTarget is EventEntry target)
        {
            ClearKanSelection();
            if (_events.Contains(target))
                ReplaceEvent(target, target.Note with { KanSwara = note.Pitch });

            return;
        }

        AddEvent(note);
    }

    private void AddEvent(SwaraEvent note)
    {
        ClearKanSelection();
        EventEntry entry = new(note, string.Empty);
        _events.Add(entry);
        EventsList.SelectedItem = entry;
        EventsList.ScrollIntoView(entry);
        UpdatePreview();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        ClearKanSelection();
        if (EventsList.SelectedItem is EventEntry entry)
        {
            int index = _events.IndexOf(entry);
            _events.RemoveAt(index);
            EventsList.SelectedIndex = Math.Min(index, _events.Count - 1);
            UpdatePreview();
        }
    }

    private void Khatka_Click(object sender, RoutedEventArgs e)
    {
        ClearKanSelection();
        if (SelectedPitch() is EventEntry entry)
            ReplaceEvent(entry, entry.Note with { Khatka = !entry.Note.Khatka });
    }

    private void Meend_Click(object sender, RoutedEventArgs e)
    {
        ClearKanSelection();
        if (SelectedPitch() is EventEntry entry)
            ReplaceEvent(entry, entry.Note with { MeendToNext = !entry.Note.MeendToNext });
    }

    private void Kan_Click(object sender, RoutedEventArgs e)
    {
        if (_kanTarget is not null)
        {
            ClearKanSelection();
            return;
        }

        if (SelectedPitch() is not EventEntry entry)
            return;

        if (entry.Note.KanSwara.HasValue)
        {
            ReplaceEvent(entry, entry.Note with { KanSwara = null });
            return;
        }

        _kanTarget = entry;
        KanButton.Content = "Cancel kan";
        Message.Text = "Choose the kan swar from the palette. You can change its saptak first.";
    }

    private EventEntry? SelectedPitch()
    {
        if (EventsList.SelectedItem is EventEntry { Note.Pitch: not null } entry)
            return entry;

        Message.Text = "Select a pitched swara first.";
        return null;
    }

    private void ReplaceEvent(EventEntry entry, SwaraEvent note)
    {
        int index = _events.IndexOf(entry);
        EventEntry replacement = new(note, entry.Duration);
        _events[index] = replacement;
        EventsList.SelectedItem = replacement;
        Message.Text = string.Empty;
        UpdatePreview();
    }

    private void ClearKanSelection()
    {
        if (_kanTarget is null)
            return;

        _kanTarget = null;
        KanButton.Content = "Kan swar";
        Message.Text = string.Empty;
    }

    private void ContinueLyric_Click(object sender, RoutedEventArgs e)
    {
        int caret = LyricsBox.SelectionStart;
        LyricsBox.SelectedText = "S";
        LyricsBox.CaretIndex = caret + 1;
        LyricsBox.Focus();
    }

    private void Preview_Changed(object sender, RoutedEventArgs e)
        => UpdatePreview();

    private void UpdatePreview()
    {
        if (Preview is not null && LyricsBox is not null)
            Preview.Content = new MaatraDisplay(_events.Select(entry => entry.Note).ToArray(),
                LyricsBox.Text, GroupBox.IsChecked == true, preview: true);
    }
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        int? startBeat = null;
        if (!string.IsNullOrWhiteSpace(StartBeatBox.Text))
        {
            if (!int.TryParse(StartBeatBox.Text, out int beat) || beat < 1 || beat > _cycleLength)
            {
                Message.Text = $"Enter a start beat from 1 to {_cycleLength}, or leave it blank.";
                return;
            }

            startBeat = beat;
        }

        if (PageBox.SelectedItem is not int page || string.IsNullOrWhiteSpace(SectionBox.Text))
        {
            Message.Text = "Enter a section name and choose a source page.";
            return;
        }

        string text = string.Join(" ", _events.Select(entry => string.IsNullOrWhiteSpace(entry.Duration)
            ? entry.Token : $"{entry.Token}:{entry.Duration.Trim()}"));

        try
        {
            _row.Swaras = MaatraNotation.Format(MaatraNotation.Parse(text));
            _row.Section = SectionBox.Text.Trim();
            _row.StartBeat = startBeat;
            _row.SourcePage = page;
            _row.Lyrics = LyricsBox.Text;
            _row.Annotation = NotesBox.Text;
            _row.ShowGrouping = GroupBox.IsChecked == true;
            DialogResult = true;
        }
        catch (FormatException exception)
        {
            Message.Text = exception.Message;
        }
    }

    #endregion

    public sealed record EventEntry(SwaraEvent Note, string InitialDuration)
    {
        public string Token => MaatraNotation.Format([Note with { Duration = new MaatraFraction(1, 1) }]);

        public FrameworkElement Glyph => SwaraGlyph.Create(Note);

        public string Duration { get; set; } = InitialDuration;
    }
}
