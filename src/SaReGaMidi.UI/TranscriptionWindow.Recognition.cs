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

        if (!_canEdit || _busy || _rows.Count > 0)
            return;

        if (TaalBox.SelectedItem is not Taal taal)
        {
            StatusText.Text = "Choose the composition's taal before generating a draft.";
            TaalBox.Focus();
            return;
        }

        using CancellationTokenSource cancellation = new();
        _recognitionCancellation = cancellation;
        _busy = true;
        GenerateDraftButton.Content = "_Cancel draft";
        StatusText.Text = "Reading the ordered scans on this PC.";
        UpdateActions();

        try
        {
            string title = TitleBox.Text;
            RecognitionDraft draft = await Task.Run(
                () => ScanRecognition.GenerateAsync(_workspace.Pages, taal, title, cancellation.Token),
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
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
            GenerateDraftButton.Content = "_Generate draft";
            UpdateActions();
        }
    }

    private void ApplyRecognitionDraft(RecognitionDraft draft)
    {
        DisplayScore(draft.Score, restoreReferenceLayout: false);
        ScoreTabs.SelectedIndex = 0;
        _dirty = true;
        GenerateDraftButton.Visibility = Visibility.Collapsed;
        ReferenceButton.Visibility = Visibility.Collapsed;
        string missing = draft.MissingPages.Count == 0
            ? string.Empty : $" No notation detected on source page(s) {string.Join(", ", draft.MissingPages)}.";
        StatusText.Text = $"Draft ready. Check the score against the scans, including {draft.ReviewCount} flagged maatras.{missing}";
    }
}
