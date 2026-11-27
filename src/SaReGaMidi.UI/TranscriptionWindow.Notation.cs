using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using SaReGaMidi.Music.Rhythm;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

public partial class TranscriptionWindow
{
    private void ScoreTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != ScoreTabs || MaatraGrid is null)
            return;

        if (ScoreTabs.SelectedIndex != 0)
            return;

        if (!CommitGrid())
        {
            ScoreTabs.SelectedIndex = 1;
            return;
        }

        UpdateBeats();
    }

    private void RenderNotation()
    {
        if (NotationSheet is null)
            return;

        NotationSheet.Children.Clear();
        if (_rows.Count == 0)
        {
            TextBlock empty = new() { Text = "Choose a taal, then add the first maatra." };
            empty.SetResourceReference(StyleProperty, "MutedText");
            NotationSheet.Children.Add(empty);
            return;
        }

        Taal? taal = TaalBox.SelectedItem as Taal;
        Dictionary<int, string> marks = [];
        int lineLength = taal is null ? 12 : taal.MaatraCount * (taal.MaatraCount <= 8 ? 2 : 1);
        int firstBeat = 1;

        if (taal is not null)
        {
            foreach (Vibhaag division in taal.Vibhaags)
            {
                marks.Add(firstBeat, division.Mark);
                firstBeat += division.Theka.Count;
            }
        }

        List<(Grid Grid, List<MeendAnchor> Anchors, bool StartsSection)> lines = [];
        Grid line = new();
        List<MeendAnchor> anchors = [];
        int columns = 0;
        string? section = null;

        for (int index = 0; index < _rows.Count; index++)
        {
            TranscriptionRow row = _rows[index];
            bool newSection = index == 0 || row.StartBeat.HasValue
                || !string.Equals(section, row.Section, StringComparison.OrdinalIgnoreCase);
            int beat = int.TryParse(row.Beat, out int cycleBeat) ? cycleBeat : 0;

            if (newSection || row.StartsPrintedLine == true || (row.StartsPrintedLine is null && columns >= lineLength))
            {
                if (newSection && !string.IsNullOrWhiteSpace(row.Section))
                {
                    TextBlock heading = new()
                    {
                        Text = RanjishHiSahiDraft.DisplaySectionName(row.Section),
                        FontFamily = new FontFamily("Nirmala UI"),
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 8, 0, 4)
                    };
                    NotationSheet.Children.Add(heading);
                }

                line = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) };
                NotationSheet.Children.Add(line);
                anchors = [];
                lines.Add((line, anchors, newSection));
                columns = 0;

                if ((newSection || row.StartsPrintedLine == true) && beat > 1)
                {
                    for (; columns < beat - 1; columns++)
                        AddNotationColumn(line, columns);
                }
            }

            marks.TryGetValue(beat, out string? mark);
            AddNotationColumn(line, columns);
            FrameworkElement cell = CreateNotationCell(row, mark, index, anchors,
                showDivider: line.Children.Count > 0 && mark is not null);
            Grid.SetColumn(cell, columns);
            line.Children.Add(cell);
            columns++;
            section = row.Section;
        }

        Dictionary<Guid, (MeendAnchor Anchor, int Line)> endpoints = [];
        List<MeendSegment>[] spans = new List<MeendSegment>[lines.Count];
        for (int index = 0; index < lines.Count; index++)
        {
            spans[index] = [];
            foreach (MeendAnchor anchor in lines[index].Anchors)
            {
                if (anchor.Note.AnchorId is Guid id)
                    endpoints.TryAdd(id, (anchor, index));
            }
        }

        for (int index = 0; index < lines.Count; index++)
        {
            foreach (MeendAnchor anchor in lines[index].Anchors)
            {
                if (anchor.Note.MeendTargetId is not Guid target
                    || !endpoints.TryGetValue(target, out var end) || end.Line < index)
                {
                    continue;
                }

                for (int lineIndex = index; lineIndex <= end.Line; lineIndex++)
                {
                    spans[lineIndex].Add(new MeendSegment(
                        lineIndex == index ? anchor : null, lineIndex == end.Line ? end.Anchor : null));
                }
            }
        }

        bool incoming = false;
        for (int index = 0; index < lines.Count; index++)
        {
            (Grid grid, List<MeendAnchor> lineAnchors, bool startsSection) = lines[index];
            bool outgoing = index + 1 < lines.Count && !lines[index + 1].StartsSection
                && lineAnchors.LastOrDefault(anchor => anchor.Note.Kind != SwaraEventKind.Sustain)?.Note.MeendToNext == true
                && lines[index + 1].Anchors.FirstOrDefault(anchor => anchor.Note.Kind != SwaraEventKind.Sustain)?.Note.Kind == SwaraEventKind.Note;
            if (incoming || outgoing || spans[index].Count > 0 || lineAnchors.Any(anchor => anchor.Note.MeendToNext))
            {
                MeendLayer layer = new(lineAnchors, outgoing, incoming && !startsSection, spans[index]);
                Grid.SetColumnSpan(layer, grid.ColumnDefinitions.Count);
                grid.Children.Add(layer);
            }

            incoming = outgoing;
        }
    }

    private static void AddNotationColumn(Grid line, int index)
        => line.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto,
            MinWidth = 52,
            SharedSizeGroup = $"Maatra{index}"
        });

    private FrameworkElement CreateNotationCell(TranscriptionRow row, string? mark, int index,
        List<MeendAnchor> anchors, bool showDivider)
    {
        StackPanel content = new();
        TextBlock beatMark = new()
        {
            Text = mark ?? " ",
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 12
        };
        bool needsReview = row.Annotation.StartsWith("Review: ", StringComparison.Ordinal);
        if (row.Swaras.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("?"))
        {
            Grid marks = new();
            marks.Children.Add(beatMark);
            TextBlock flag = new()
            {
                Text = "!",
                HorizontalAlignment = HorizontalAlignment.Right,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            };
            flag.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            marks.Children.Add(flag);
            content.Children.Add(marks);
        }
        else
        {
            content.Children.Add(beatMark);
        }

        bool blank = row.IsBlank && string.IsNullOrWhiteSpace(row.Swaras);
        IReadOnlyList<SwaraEvent> events;
        try
        {
            events = blank ? [] : MaatraNotation.Parse(row.Swaras);
        }
        catch (FormatException)
        {
            events = string.IsNullOrWhiteSpace(row.Swaras) ? [] : MaatraNotation.Parse("?");
        }

        MaatraDisplay display = new(events, row.Lyrics, row.ShowGrouping, blank: blank)
        {
            Margin = new Thickness(0, 6, 0, 0)
        };
        content.Children.Add(display);
        anchors.AddRange(display.Anchors);

        Button cell = new()
        {
            Content = content,
            MinWidth = 52,
            Padding = new Thickness(3, 2, 3, 3),
            Tag = index,
            ToolTip = row.BorrowedFrom is TranscriptionRow source && blank
                ? $"Blank in print. Uses the swaras from maatra {_rows.IndexOf(source) + 1} during the pass."
                : string.IsNullOrWhiteSpace(row.Annotation) ? "Edit maatra" : row.Annotation
        };
        cell.SetResourceReference(StyleProperty, "NotationCell");
        if (MaatraGrid.SelectedIndex == index)
            cell.SetResourceReference(BackgroundProperty, "SelectionBrush");

        string beatLabel = string.IsNullOrEmpty(row.Beat) ? string.Empty : $", beat {row.Beat}";
        string reviewLabel = needsReview ? $", {row.Annotation}" : string.Empty;
        AutomationProperties.SetName(cell, $"Maatra {index + 1}{beatLabel}, {row.Swaras}, {row.Lyrics}{reviewLabel}");
        cell.Click += NotationCell_Click;

        Border boundary = new()
        {
            Child = cell,
            BorderThickness = new Thickness(showDivider ? 1 : 0, 0, 0, 0)
        };
        boundary.SetResourceReference(Border.BorderBrushProperty, "MutedBrush");
        return boundary;
    }

    private void NotationCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index })
            EditMaatra(index);
    }

    private void EditMaatra(int index)
    {
        if (TaalBox.SelectedItem is not Taal taal)
        {
            StatusText.Text = "Choose a taal before editing a maatra.";
            return;
        }

        MaatraGrid.SelectedIndex = index;
        TranscriptionRow row = _rows[index];
        TranscriptionRow? earlierBeat = _rows.Take(index).LastOrDefault(earlier => earlier.Beat == row.Beat
            && string.Equals(earlier.Section, row.Section, StringComparison.OrdinalIgnoreCase)
            && !earlier.IsBlank && !string.IsNullOrWhiteSpace(earlier.Swaras));
        MaatraEditor editor = new(row, _workspace.Pages.Count, taal.MaatraCount,
            row.BorrowedFrom ?? earlierBeat, GetMeendDestinations(index)) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            MaatraGrid.Items.Refresh();
            SourcePages.SelectedIndex = _rows[index].SourcePage - 1;
            MarkEdited();
        }

        UpdateBeats();
    }

    private IReadOnlyList<MeendDestination> GetMeendDestinations(int index)
    {
        List<MeendDestination> destinations = [];
        for (int later = index + 1; later < _rows.Count; later++)
        {
            TranscriptionRow row = _rows[later];
            IReadOnlyList<SwaraEvent> swaras;
            try
            {
                swaras = row.IsBlank ? [] : MaatraNotation.Parse(row.Swaras);
            }
            catch (FormatException)
            {
                continue;
            }

            for (int noteIndex = 0; noteIndex < swaras.Count; noteIndex++)
            {
                if (swaras[noteIndex].Pitch is not { } pitch)
                    continue;

                Guid id = swaras[noteIndex].AnchorId ?? Guid.NewGuid();
                destinations.Add(new MeendDestination(row, noteIndex, id, later - index,
                    $"Maatra {later + 1} · {pitch.Swara} ({noteIndex + 1})"));
            }
        }

        return destinations;
    }

    private sealed record PassSelection(string Name, TranscriptionRow First, TranscriptionRow Last);
}

internal sealed record MeendDestination(TranscriptionRow Row, int NoteIndex, Guid Id, int Distance, string Label);
