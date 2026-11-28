using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SaReGaMidi.Music.Rhythm;

namespace SaReGaMidi.UI;

public partial class TranscriptionWindow
{
    private CancellationTokenSource? _recognitionCancellation;

    private async void GenerateDraft_Click(object sender, RoutedEventArgs e)
    {
        if (_recognitionCancellation is not null)
        {
            _recognitionCancellation.Cancel();
            return;
        }

        if (!_canEdit || _busy || !CommitGrid())
            return;

        if (TaalBox.SelectedItem is not Taal taal)
        {
            StatusText.Text = "Choose the composition's taal before generating a draft.";
            TaalBox.Focus();
            return;
        }

        if (_rows.Count > 0 && MessageBox.Show(this,
            "Generate a fresh draft from the scans and replace this transcription, including any edits?",
            Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        using CancellationTokenSource cancellation = new();
        _recognitionCancellation = cancellation;
        _busy = true;
        RecognitionProgress.Visibility = Visibility.Visible;
        StatusText.Text = "Reading the ordered scans on this PC.";
        UpdateActions();

        try
        {
            string title = TitleBox.Text;
            RecognitionDraft draft = await Task.Run(
                () => ScanRecognition.GenerateAsync(_workspace.Pages, taal, title, cancellation.Token),
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            GenerateDraftButton.IsEnabled = false;
            StatusText.Text = "Saving the generated draft...";
            await _scoreFile.SaveAsync(draft.Score);
            ApplyRecognitionDraft(draft);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Draft generation cancelled.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or Win32Exception
            or FormatException or ArgumentException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            StatusText.Text = $"Could not generate a draft. {exception.Message}";
        }
        finally
        {
            _recognitionCancellation = null;
            _busy = false;
            RecognitionProgress.Visibility = Visibility.Collapsed;
            UpdateActions();
        }
    }

    private void ApplyRecognitionDraft(RecognitionDraft draft)
    {
        DisplayScore(draft.Score, restoreReferenceLayout: false);
        ScoreTabs.SelectedIndex = 0;
        _dirty = false;
        Saved = true;
        ReferenceButton.Visibility = Visibility.Collapsed;
        string missing = draft.MissingPages.Count == 0
            ? string.Empty : $" No notation detected on source page(s) {string.Join(", ", draft.MissingPages)}.";
        StatusText.Text = $"Draft saved. Check the score against the scans, including {draft.ReviewCount} flagged maatras.{missing}";
    }
}
