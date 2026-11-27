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

        UpdatePassControls();
        if (ScoreTabs.SelectedIndex != 0)
            return;

        if (!CommitGrid())
        {
            ScoreTabs.SelectedIndex = 1;
            return;
        }

        UpdateBeats();
    }

    private void PassBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MaatraGrid is null)
            return;

        UpdatePassControls();

        if (PassBox.SelectedItem is PassSelection { First: not null } pass)
            MaatraGrid.SelectedItem = pass.First;

        RenderNotation();
    }

    private void UpdatePassControls()
    {
        bool notation = ScoreTabs.SelectedIndex == 0;
        PassControls.Visibility = notation && _passes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        RowControls.IsEnabled = !notation || PassBox.SelectedIndex <= 0;
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

        PassSelection? pass = PassBox.SelectedItem as PassSelection;
        int first = pass?.First is TranscriptionRow firstRow ? _rows.IndexOf(firstRow) : 0;
        int last = pass?.Last is TranscriptionRow lastRow ? _rows.IndexOf(lastRow) : _rows.Count - 1;

        for (int index = first; index <= last; index++)
        {
            TranscriptionRow row = _rows[index];
            bool newSection = index == first || row.StartBeat.HasValue
                || !string.Equals(section, row.Section, StringComparison.OrdinalIgnoreCase);
            int beat = int.TryParse(row.Beat, out int cycleBeat) ? cycleBeat : 0;

            if (newSection || columns >= lineLength)
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

                if (newSection && beat > 1)
                {
                    for (; columns < beat - 1; columns++)
                        AddNotationColumn(line, columns);
                }
            }

            marks.TryGetValue(beat, out string? mark);
            AddNotationColumn(line, columns);
            FrameworkElement cell = CreateNotationCell(row, mark, index, anchors);
            Grid.SetColumn(cell, columns);
            line.Children.Add(cell);
            columns++;
            section = row.Section;
        }

        bool incoming = false;
        for (int index = 0; index < lines.Count; index++)
        {
            (Grid grid, List<MeendAnchor> lineAnchors, bool startsSection) = lines[index];
            bool outgoing = index + 1 < lines.Count && !lines[index + 1].StartsSection
                && lineAnchors.LastOrDefault(anchor => anchor.Note.Kind != SwaraEventKind.Sustain)?.Note.MeendToNext == true
                && lines[index + 1].Anchors.FirstOrDefault(anchor => anchor.Note.Kind != SwaraEventKind.Sustain)?.Note.Kind == SwaraEventKind.Note;
            if (incoming || outgoing || lineAnchors.Any(anchor => anchor.Note.MeendToNext))
            {
                MeendLayer layer = new(lineAnchors, outgoing, incoming && !startsSection);
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

    private FrameworkElement CreateNotationCell(TranscriptionRow row, string? mark, int index, List<MeendAnchor> anchors)
    {
        StackPanel content = new();
        content.Children.Add(new TextBlock
        {
            Text = mark ?? " ",
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 12
        });

        IReadOnlyList<SwaraEvent> events;
        try
        {
            events = MaatraNotation.Parse(row.Swaras);
        }
        catch (FormatException)
        {
            events = string.IsNullOrWhiteSpace(row.Swaras) ? [] : MaatraNotation.Parse("?");
        }

        MaatraDisplay display = new(events, row.Lyrics, row.ShowGrouping) { Margin = new Thickness(0, 6, 0, 0) };
        content.Children.Add(display);
        anchors.AddRange(display.Anchors);

        Button cell = new()
        {
            Content = content,
            MinWidth = 52,
            Padding = new Thickness(3, 2, 3, 3),
            Tag = index,
            ToolTip = string.IsNullOrWhiteSpace(row.Annotation) ? "Edit maatra" : row.Annotation
        };
        cell.SetResourceReference(StyleProperty, "NotationCell");
        if (MaatraGrid.SelectedIndex == index)
            cell.SetResourceReference(BackgroundProperty, "SelectionBrush");

        string beatLabel = string.IsNullOrEmpty(row.Beat) ? string.Empty : $", beat {row.Beat}";
        AutomationProperties.SetName(cell, $"Maatra {index + 1}{beatLabel}, {row.Swaras}, {row.Lyrics}");
        cell.Click += NotationCell_Click;

        Border boundary = new()
        {
            Child = cell,
            BorderThickness = new Thickness(mark is null ? 0 : 1, 0, 0, 0)
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
        MaatraEditor editor = new(_rows[index], _workspace.Pages.Count, taal.MaatraCount) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            MaatraGrid.Items.Refresh();
            SourcePages.SelectedIndex = _rows[index].SourcePage - 1;
            MarkEdited();
        }

        UpdateBeats();
    }

    private sealed record PassSelection(string Name, TranscriptionRow? First, TranscriptionRow? Last);
}
