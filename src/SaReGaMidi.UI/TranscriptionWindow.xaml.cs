using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using SaReGaMidi.Music.Rhythm;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

public partial class TranscriptionWindow : Window
{
    #region Fields

    private readonly CompositionWorkspace _workspace;
    private readonly ScoreFile _scoreFile;
    private readonly ObservableCollection<TranscriptionRow> _rows = [];
    private readonly List<PassSelection> _passes = [];
    private bool _busy;
    private bool _canEdit;
    private bool _dirty;

    #endregion

    #region Constructors

    internal TranscriptionWindow(CompositionWorkspace workspace, int selectedPage)
    {
        _workspace = workspace;
        _scoreFile = new ScoreFile(workspace.FolderPath, workspace.Pages.Count);
        InitializeComponent();

        SourcePages.ItemsSource = workspace.Pages.Select((page, index) => $"{index + 1:D2}   {page.OriginalName}").ToArray();
        SourcePages.SelectedIndex = Math.Max(0, selectedPage);
        TaalBox.ItemsSource = TaalCatalog.All;
        MaatraGrid.ItemsSource = _rows;
        AnalysisSa.ItemsSource = Enumerable.Range(48, 36).Select(note => Music.WesternNoteNames.Format(note)).ToArray();
        AnalysisSa.SelectedIndex = 61 - 48;
        UpdateBeats();
    }

    #endregion

    #region Properties

    internal bool Saved { get; private set; }

    #endregion

    #region Methods

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_busy)
        {
            e.Cancel = true;
            return;
        }

        if (!_dirty && !CommitGrid())
            _dirty = true;

        if (_dirty && !e.Cancel)
        {
            e.Cancel = MessageBox.Show(this, "Close and discard the unsaved score changes?", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes;
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= Window_Loaded;
        _busy = true;
        StatusText.Text = "Opening the score...";

        try
        {
            CompositionScore? score = await _scoreFile.LoadAsync();
            if (score is not null)
                DisplayScore(score);

            _canEdit = true;
            GenerateDraftButton.Visibility = Visibility.Visible;
            ReferenceButton.Visibility = score is null && RanjishHiSahiDraft.Matches(_workspace.Pages)
                ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = score is null
                ? "Choose a taal, then generate a draft or enter the first maatra."
                : "Score loaded. Select a maatra to find its source page.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or JsonException or FormatException)
        {
            StatusText.Text = $"Could not open score.json. {exception.Message}";
        }
        finally
        {
            _busy = false;
            UpdateActions();
        }
    }

    private void Reference_Click(object sender, RoutedEventArgs e)
    {
        if (!CommitGrid() || (_dirty && MessageBox.Show(this, "Replace the current unsaved draft?", Title,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes))
        {
            return;
        }

        DisplayScore(RanjishHiSahiDraft.Create());
        ReferenceButton.Visibility = Visibility.Collapsed;
        _dirty = true;
        UpdateActions();
        StatusText.Text = "Draft loaded for all three pages. Review uncertain swaras, ornaments, and return cues before saving.";
    }

    private void ExportMidi_Click(object sender, RoutedEventArgs e)
    {
        if (!_canEdit || _busy || !CommitGrid())
            return;
        try
        {
            CompositionScore score = BuildScore();
            MidiExportWindow? options = ChooseMidiSettings(score, playback: false);
            if (options is null)
                return;

            byte[] midi = MidiScoreExport.Render(score, options.Passage, options.Sa, options.Tempo);
            string folder = Path.Combine(_workspace.FolderPath, "artifacts");
            Directory.CreateDirectory(folder);
            Microsoft.Win32.SaveFileDialog dialog = new()
            {
                Title = "Export MIDI", InitialDirectory = folder, Filter = "MIDI file (*.mid)|*.mid",
                DefaultExt = ".mid", FileName = string.Join("_", score.Title.Split(Path.GetInvalidFileNameChars()))
            };
            if (dialog.ShowDialog(this) != true)
                return;

            string temporary = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, $"{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(temporary, midi);
                File.Move(temporary, dialog.FileName, true);
            }
            finally
            {
                File.Delete(temporary);
            }
            StatusText.Text = $"Exported MIDI to {dialog.FileName}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or FormatException or ArgumentException or OverflowException)
        {
            StatusText.Text = $"Could not export MIDI. {exception.Message}";
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_canEdit || _busy || !CommitGrid())
            return;

        _busy = true;
        UpdateActions();

        try
        {
            await _scoreFile.SaveAsync(BuildScore());
            _dirty = false;
            Saved = true;
            ReferenceButton.Visibility = Visibility.Collapsed;
            StatusText.Text = $"Saved {_rows.Count} maatras to score.json.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or FormatException)
        {
            StatusText.Text = exception.Message;
        }
        finally
        {
            _busy = false;
            UpdateActions();
        }
    }

    private void DisplayScore(CompositionScore score, bool restoreReferenceLayout = true)
    {
        if (restoreReferenceLayout && RanjishHiSahiDraft.Matches(_workspace.Pages))
            score = RanjishHiSahiDraft.RestoreBorrowedPickup(
                RanjishHiSahiDraft.RestorePrintedLines(RanjishHiSahiDraft.RestorePasses(score)));

        _passes.Clear();
        TitleBox.Text = score.Title;
        TaalBox.SelectedItem = TaalCatalog.FindByName(score.TaalName);
        _rows.Clear();

        foreach (ScoreSection section in score.Sections)
        {
            int? startBeat = section.StartBeat;
            foreach (ScoreMaatra maatra in section.Maatras)
            {
                _rows.Add(new TranscriptionRow
                {
                    SourcePage = maatra.SourcePage,
                    Section = RanjishHiSahiDraft.DisplaySectionName(section.Name),
                    StartBeat = startBeat,
                    Swaras = maatra.Swaras.Count == 0 ? string.Empty : MaatraNotation.Format(maatra.Swaras),
                    IsBlank = maatra.Swaras.Count == 0,
                    BorrowedFrom = maatra.BorrowedFrom is int source ? _rows[source] : null,
                    Lyrics = maatra.Lyrics,
                    ShowGrouping = maatra.ShowGrouping,
                    StartsPrintedLine = maatra.StartsPrintedLine,
                    Annotation = maatra.Annotation
                });

                startBeat = null;
            }
        }

        foreach (ScorePass pass in score.Passes)
            _passes.Add(new PassSelection(pass.Name, _rows[pass.StartMaatra], _rows[pass.StartMaatra + pass.MaatraCount - 1]));

        MaatraGrid.SelectedIndex = 0;
        UpdateBeats();
    }

    private CompositionScore BuildScore()
    {
        if (TaalBox.SelectedItem is not Taal taal)
            throw new InvalidDataException("Choose a taal before saving the score.");

        List<ScoreSection> sections = [];
        string? currentName = null;
        List<ScoreMaatra> maatras = [];

        for (int index = 0; index < _rows.Count; index++)
        {
            TranscriptionRow row = _rows[index];
            string sectionName = row.Section.Trim();
            if (string.IsNullOrWhiteSpace(sectionName))
                throw new InvalidDataException($"Row {index + 1} needs a section name.");

            if (!string.Equals(sectionName, currentName, StringComparison.OrdinalIgnoreCase) || row.StartBeat.HasValue)
            {
                if (row.StartBeat is not int startBeat || startBeat < 1 || startBeat > taal.MaatraCount)
                    throw new InvalidDataException($"Enter a start beat from 1 to {taal.MaatraCount} in row {index + 1}.");

                currentName = sectionName;
                maatras = [];
                sections.Add(new ScoreSection(sectionName, startBeat, maatras.AsReadOnly()));
            }

            try
            {
                IReadOnlyList<SwaraEvent> events = row.IsBlank && string.IsNullOrWhiteSpace(row.Swaras)
                    ? [] : MaatraNotation.Parse(row.Swaras);
                maatras.Add(new ScoreMaatra(row.SourcePage, events, row.Lyrics, row.Annotation)
                {
                    ShowGrouping = row.ShowGrouping,
                    StartsPrintedLine = row.StartsPrintedLine,
                    BorrowedFrom = events.Count == 0 && row.BorrowedFrom is TranscriptionRow source
                        ? _rows.IndexOf(source) : null
                });
            }
            catch (FormatException exception)
            {
                throw new FormatException($"Row {index + 1}: {exception.Message}", exception);
            }
        }

        return new CompositionScore(TitleBox.Text.Trim(), taal.Name, sections.AsReadOnly())
        {
            Passes = _passes.Select(pass =>
            {
                int start = _rows.IndexOf(pass.First);
                int end = _rows.IndexOf(pass.Last);
                return new ScorePass(pass.Name, start, end - start + 1);
            }).ToArray()
        };
    }

    private void Add_Click(object sender, RoutedEventArgs e)
        => InsertRow(_rows.Count);

    private void Insert_Click(object sender, RoutedEventArgs e)
        => InsertRow(MaatraGrid.SelectedIndex >= 0 ? MaatraGrid.SelectedIndex : _rows.Count);

    private void InsertRow(int index)
    {
        if (!CommitGrid())
            return;

        TranscriptionRow? neighbor = index < _rows.Count ? _rows[index] : _rows.LastOrDefault();
        TranscriptionRow row = new()
        {
            SourcePage = SourcePages.SelectedIndex + 1,
            Section = neighbor?.Section ?? string.Empty,
            StartsPrintedLine = index < _rows.Count ? neighbor?.StartsPrintedLine : null,
            StartBeat = neighbor is null ? 1 : index < _rows.Count ? neighbor.StartBeat : null
        };

        if (index < _rows.Count && neighbor is not null)
        {
            neighbor.StartBeat = null;
            if (neighbor.StartsPrintedLine == true)
                neighbor.StartsPrintedLine = false;
        }

        _rows.Insert(index, row);
        MaatraGrid.Items.Refresh();
        MaatraGrid.SelectedItem = row;
        MaatraGrid.ScrollIntoView(row);
        MarkEdited();
        UpdateBeats();

        if (ScoreTabs.SelectedIndex == 0)
            EditMaatra(index);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (!CommitGrid() || MaatraGrid.SelectedIndex < 0)
            return;

        int index = MaatraGrid.SelectedIndex;
        TranscriptionRow removed = _rows[index];
        if (_rows.Any(row => row.BorrowedFrom == removed))
        {
            StatusText.Text = "Another maatra borrows the swaras from this row. Clear that link before removing it.";
            return;
        }

        if (_passes.Any(pass => pass.First == removed || pass.Last == removed))
        {
            StatusText.Text = "This maatra marks the start or end of a repeat pass. Its notes can still be edited.";
            return;
        }
        _rows.RemoveAt(index);

        if (removed.StartsPrintedLine == true && index < _rows.Count
            && string.Equals(removed.Section, _rows[index].Section, StringComparison.OrdinalIgnoreCase))
        {
            _rows[index].StartsPrintedLine = true;
        }

        if (removed.StartBeat.HasValue && index < _rows.Count && _rows[index].StartBeat is null
            && string.Equals(removed.Section, _rows[index].Section, StringComparison.OrdinalIgnoreCase))
        {
            _rows[index].StartBeat = removed.StartBeat;
        }

        MaatraGrid.Items.Refresh();
        MaatraGrid.SelectedIndex = Math.Min(index, _rows.Count - 1);
        MarkEdited();
        UpdateBeats();
    }

    private bool CommitGrid()
    {
        bool committed = MaatraGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true)
            && MaatraGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        if (!committed)
            StatusText.Text = "Correct the highlighted cell before continuing.";

        return committed;
    }

    private void SourcePages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int index = SourcePages.SelectedIndex;
        if (index >= 0)
            ScanImage.Source = _workspace.Pages[index].Preview;

        ScanScroll.ScrollToTop();
    }

    private void MaatraGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MaatraGrid.SelectedItem is TranscriptionRow row && row.SourcePage >= 1 && row.SourcePage <= _workspace.Pages.Count)
            SourcePages.SelectedIndex = row.SourcePage - 1;
    }

    private void TitleBox_TextChanged(object sender, TextChangedEventArgs e)
        => MarkEdited();

    private void TaalBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        MarkEdited();
        UpdateBeats();
    }

    private void MaatraGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit)
        {
            MarkEdited();
            Dispatcher.BeginInvoke(UpdateBeats);
        }
    }

    private void UpdateBeats()
    {
        int cycleLength = (TaalBox.SelectedItem as Taal)?.MaatraCount ?? 0;
        int beat = 0;
        string? section = null;

        foreach (TranscriptionRow row in _rows)
        {
            string name = row.Section.Trim();
            if (row.StartBeat.HasValue || !string.Equals(name, section, StringComparison.OrdinalIgnoreCase))
                beat = row.StartBeat.GetValueOrDefault();

            row.Beat = beat >= 1 && beat <= cycleLength ? beat.ToString() : string.Empty;
            beat = beat >= 1 && beat <= cycleLength ? (beat % cycleLength) + 1 : 0;
            section = name;
        }

        RenderNotation();
        RefreshAnalysis();
    }

    private void MarkEdited()
    {
        if (_canEdit && !_busy)
        {
            _playbackCancellation?.Cancel();
            _dirty = true;
            UpdateActions();
        }
    }

    private void UpdateActions()
    {
        PlayButton.IsEnabled = _playbackCancellation is not null || (_canEdit && !_busy && _rows.Count > 0);
        EditorPanel.IsEnabled = _canEdit && !_busy;
        GenerateDraftButton.IsEnabled = _recognitionCancellation is not null || (_canEdit && !_busy && _playbackCancellation is null);
        GenerateDraftButton.Content = _recognitionCancellation is not null ? "_Cancel draft"
            : _rows.Count > 0 ? "_Regenerate draft" : "_Generate draft";
        ReferenceButton.IsEnabled = _canEdit && !_busy;
        ExportButton.IsEnabled = _canEdit && !_busy && _rows.Count > 0;
        SaveButton.IsEnabled = _canEdit && !_busy;
    }

    #endregion
}
