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
            ReferenceButton.Visibility = score is null && RanjishHiSahiDraft.Matches(_workspace.Pages)
                ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = score is null
                ? "Choose a taal and enter the first maatra."
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

    private void DisplayScore(CompositionScore score)
    {
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
                    Section = section.Name,
                    StartBeat = startBeat,
                    Swaras = MaatraNotation.Format(maatra.Swaras),
                    Lyrics = maatra.Lyrics,
                    Annotation = maatra.Annotation
                });

                startBeat = null;
            }
        }

        UpdateBeats();
        MaatraGrid.SelectedIndex = 0;
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
                maatras.Add(new ScoreMaatra(row.SourcePage, MaatraNotation.Parse(row.Swaras), row.Lyrics, row.Annotation));
            }
            catch (FormatException exception)
            {
                throw new FormatException($"Row {index + 1}: {exception.Message}", exception);
            }
        }

        return new CompositionScore(TitleBox.Text.Trim(), taal.Name, sections.AsReadOnly());
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
            StartBeat = neighbor is null ? 1 : index < _rows.Count ? neighbor.StartBeat : null
        };

        if (index < _rows.Count && neighbor is not null)
            neighbor.StartBeat = null;

        _rows.Insert(index, row);
        MaatraGrid.Items.Refresh();
        MaatraGrid.SelectedItem = row;
        MaatraGrid.ScrollIntoView(row);
        MarkEdited();
        UpdateBeats();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (!CommitGrid() || MaatraGrid.SelectedIndex < 0)
            return;

        int index = MaatraGrid.SelectedIndex;
        TranscriptionRow removed = _rows[index];
        _rows.RemoveAt(index);

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
    }

    private void MarkEdited()
    {
        if (_canEdit && !_busy)
        {
            _dirty = true;
            UpdateActions();
        }
    }

    private void UpdateActions()
    {
        EditorPanel.IsEnabled = _canEdit && !_busy;
        ReferenceButton.IsEnabled = _canEdit && !_busy;
        SaveButton.IsEnabled = _canEdit && !_busy;
    }

    #endregion
}
