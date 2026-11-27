using System;
using System.Collections.Generic;
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
    private readonly TranscriptionRow? _earlierBeat;
    private readonly IReadOnlyList<MeendDestination> _destinations;
    private readonly ObservableCollection<EventEntry> _events = [];
    private EventEntry? _kanTarget;

    #endregion

    #region Constructors

    internal MaatraEditor(TranscriptionRow row, int pageCount, int cycleLength,
        TranscriptionRow? earlierBeat, IReadOnlyList<MeendDestination> destinations)
    {
        _row = row;
        _cycleLength = cycleLength;
        _earlierBeat = earlierBeat;
        _destinations = destinations;
        InitializeComponent();
        SectionBox.Text = row.Section;
        StartBeatBox.Text = row.StartBeat?.ToString() ?? string.Empty;
        PageBox.ItemsSource = Enumerable.Range(1, pageCount);
        PageBox.SelectedItem = row.SourcePage;
        LyricsBox.Text = row.Lyrics;
        NotesBox.Text = row.Annotation;
        GroupBox.IsChecked = row.ShowGrouping;
        BlankBox.IsChecked = row.IsBlank;
        BorrowBox.IsChecked = row.BorrowedFrom is not null;
        WholeBlankBox.IsChecked = row.IsBlank && string.IsNullOrEmpty(row.Lyrics);
        MeendDestinationBox.ItemsSource = destinations;
        MeendDestinationBox.IsEnabled = destinations.Count > 0;
        PrintedLineBox.IsChecked = row.StartsPrintedLine == true;
        EventsList.ItemsSource = _events;
        SaptakBox.ItemsSource = Enum.GetValues<Saptak>();
        SaptakBox.SelectedItem = Saptak.Madhya;

        if (string.IsNullOrWhiteSpace(row.Swaras))
        {
            UpdatePreview();
            return;
        }

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

        EventsList.SelectedIndex = _events.Count > 0 ? 0 : -1;
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
                Padding = new Thickness(3, 5, 3, 5),
                Margin = new Thickness(0, 0, 4, 0),
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

            EditorTabs.SelectedIndex = 1;
            return;
        }

        AddEvent(note);
    }

    private void AddEvent(SwaraEvent note)
    {
        ClearKanSelection();
        BlankBox.IsChecked = false;
        WholeBlankBox.IsChecked = false;
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
        if (MeendPanel.Visibility == Visibility.Visible)
        {
            MeendPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ClearKanSelection();
        if (SelectedPitch() is not EventEntry entry)
            return;

        MeendPanel.Visibility = Visibility.Visible;
        if (entry.Note.MeendTargetId is Guid target
            && _destinations.FirstOrDefault(destination => destination.Id == target) is MeendDestination destination)
        {
            MeendDistanceBox.Text = destination.Distance.ToString();
            MeendDestinationBox.SelectedItem = destination;
        }
    }

    private void NextMeend_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPitch() is EventEntry entry)
        {
            ReplaceEvent(entry, entry.Note with { MeendToNext = true, MeendTargetId = null });
            Message.Text = "Meend ends at the next swara.";
        }
    }

    private void ClearMeend_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPitch() is EventEntry entry)
            ReplaceEvent(entry, entry.Note with { MeendToNext = false, MeendTargetId = null });
    }

    private void SetMeend_Click(object sender, RoutedEventArgs e)
    {
        ClearKanSelection();
        if (SelectedPitch() is not EventEntry entry)
            return;

        if (MeendDestinationBox.SelectedItem is not MeendDestination target)
        {
            Message.Text = "Choose a later swara from the meend list.";
            return;
        }

        ReplaceEvent(entry, entry.Note with { MeendToNext = false, MeendTargetId = target.Id });
        Message.Text = $"Meend ends at {target.Label}.";
    }

    private void MeendDistance_Changed(object sender, TextChangedEventArgs e)
    {
        if (MeendDestinationBox is null)
            return;

        MeendDestination? selected = MeendDestinationBox.SelectedItem as MeendDestination;
        string input = MeendDistanceBox.Text.Trim();
        IReadOnlyList<MeendDestination> matches = _destinations;
        if (input.Length > 0)
        {
            matches = int.TryParse(input, out int distance) && distance > 0
                ? _destinations.Where(destination => destination.Distance == distance).ToArray()
                : [];
        }

        MeendDestinationBox.ItemsSource = matches;
        MeendDestinationBox.IsEnabled = matches.Count > 0;
        MeendDestinationBox.SelectedItem = matches.Count == 1 ? matches[0] : matches.FirstOrDefault(note => note == selected);
        Message.Text = input.Length > 0 && matches.Count == 0
            ? "Enter a positive maatra distance that leads to a written swara."
            : string.Empty;
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
        CancelKanButton.Visibility = Visibility.Visible;
        EditorTabs.SelectedIndex = 0;
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
        CancelKanButton.Visibility = Visibility.Collapsed;
        Message.Text = string.Empty;
    }

    private void ContinueLyric_Click(object sender, RoutedEventArgs e)
    {
        WholeBlankBox.IsChecked = false;
        int caret = LyricsBox.SelectionStart;
        LyricsBox.SelectedText = ScoreMaatra.LyricContinuationSign.ToString();
        LyricsBox.CaretIndex = caret + 1;
        LyricsBox.Focus();
    }

    private void Preview_Changed(object sender, RoutedEventArgs e)
        => UpdatePreview();

    private void UpdatePreview()
    {
        if (Preview is null || LyricsBox is null || GroupBox is null || BorrowBox is null)
            return;

        bool wholeBlank = WholeBlankBox.IsChecked == true;
        bool blank = wholeBlank || BlankBox.IsChecked == true;
        BorrowBox.IsEnabled = blank && _earlierBeat is not null;
        LyricsBox.IsEnabled = !wholeBlank;
        Preview.Content = new MaatraDisplay(blank ? [] : _events.Select(entry => entry.Note).ToArray(),
            wholeBlank ? string.Empty : LyricsBox.Text, GroupBox.IsChecked == true, preview: true, blank: blank);
    }
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        int? startBeat = null;
        if (!string.IsNullOrWhiteSpace(StartBeatBox.Text))
        {
            if (!int.TryParse(StartBeatBox.Text, out int beat) || beat < 1 || beat > _cycleLength)
            {
                EditorTabs.SelectedIndex = 2;
                Message.Text = $"Enter a start beat from 1 to {_cycleLength}, or leave it blank.";
                return;
            }

            startBeat = beat;
        }

        if (PageBox.SelectedItem is not int page || string.IsNullOrWhiteSpace(SectionBox.Text))
        {
            EditorTabs.SelectedIndex = 2;
            Message.Text = "Enter a section name and choose a source page.";
            return;
        }

        string text = string.Join(" ", _events.Select(entry => string.IsNullOrWhiteSpace(entry.Duration)
            ? entry.Token : $"{entry.Token}:{entry.Duration.Trim()}"));

        try
        {
            bool wholeBlank = WholeBlankBox.IsChecked == true;
            bool blank = wholeBlank || BlankBox.IsChecked == true;
            IReadOnlyList<SwaraEvent> swaras = blank ? [] : MaatraNotation.Parse(text);
            foreach (MeendDestination target in _destinations.Where(target => swaras.Any(note => note.MeendTargetId == target.Id)))
            {
                SwaraEvent[] targetSwaras = MaatraNotation.Parse(target.Row.Swaras).ToArray();
                targetSwaras[target.NoteIndex] = targetSwaras[target.NoteIndex] with { AnchorId = target.Id };
                target.Row.Swaras = MaatraNotation.Format(targetSwaras);
            }

            _row.Swaras = blank ? string.Empty : MaatraNotation.Format(swaras);
            _row.IsBlank = blank;
            _row.BorrowedFrom = blank && BorrowBox.IsChecked == true ? _earlierBeat : null;
            _row.Section = SectionBox.Text.Trim();
            _row.StartBeat = startBeat;
            _row.SourcePage = page;
            _row.Lyrics = wholeBlank ? string.Empty : LyricsBox.Text;
            _row.Annotation = NotesBox.Text;
            _row.ShowGrouping = GroupBox.IsChecked == true;
            bool startsPrintedLine = PrintedLineBox.IsChecked == true;
            if (startsPrintedLine != (_row.StartsPrintedLine == true))
                _row.StartsPrintedLine = startsPrintedLine;
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
