using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SaReGaMidi.UI;

public partial class MainWindow : Window
{
    private CompositionWorkspace? _workspace;

    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = (Application.Current as App)?.StartupMessage ?? "Ready to import a composition.";
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        if (Application.Current is not App application || !application.Settings.StartWithPreviousWorkspace)
            return;

        WorkspaceSettings settings = application.Settings;
        StartImportButton.IsEnabled = false;
        OpenWorkspaceButton.IsEnabled = false;
        StatusText.Text = "Checking for the previous workspace...";

        try
        {
            CompositionWorkspace? workspace = await Task.Run(() =>
                CompositionWorkspace.Reopen(settings.WorkFolder, settings.PreviousWorkspace));

            if (workspace is null)
            {
                StatusText.Text = application.StartupMessage ?? "Ready to import a composition.";
                return;
            }

            ShowWorkspace(workspace);
            StatusText.Text = $"Reopened the previous workspace with {PageCountText.Text}.";

            if (settings.PreviousWorkspace is null)
                RememberWorkspace(application, workspace);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
            or ArgumentException or NotSupportedException or FormatException or InvalidDataException)
        {
            StatusText.Text = "Could not reopen the previous workspace. Started with a blank workspace.";
        }
        finally
        {
            StartImportButton.IsEnabled = true;
            OpenWorkspaceButton.IsEnabled = true;
        }
    }

    private void ImportComposition_Click(object sender, RoutedEventArgs e)
    {
        App application = (App)Application.Current;
        ImportWindow importer = new(application.Settings.WorkFolder) { Owner = this };

        if (importer.ShowDialog() != true || importer.Workspace is not CompositionWorkspace workspace)
            return;

        ShowWorkspace(workspace);
        StatusText.Text = $"Imported {PageCountText.Text}. The scans are saved and ready for transcription.";
        RememberWorkspace(application, workspace);
    }

    private async void OpenWorkspace_Click(object sender, RoutedEventArgs e)
    {
        App application = (App)Application.Current;
        WorkspacePicker picker = new(application.Settings.WorkFolder) { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedFolder is not string folder)
            return;

        IsEnabled = false;
        StatusText.Text = "Opening workspace...";
        try
        {
            CompositionWorkspace workspace = await Task.Run(() => CompositionWorkspace.Open(folder));
            ShowWorkspace(workspace);
            StatusText.Text = $"Opened {Path.GetFileName(folder)} with {PageCountText.Text}.";
            RememberWorkspace(application, workspace);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
            or ArgumentException or NotSupportedException or FormatException or InvalidDataException)
        {
            StatusText.Text = $"Could not open the workspace. {exception.Message}";
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void RememberWorkspace(App application, CompositionWorkspace workspace)
    {
        try
        {
            application.RememberWorkspace(workspace.FolderPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText.Text += " The previous workspace preference could not be saved.";
        }
    }

    private void ShowWorkspace(CompositionWorkspace workspace)
    {
        _workspace = workspace;
        WelcomeCard.Visibility = Visibility.Collapsed;
        ReviewPanel.Visibility = Visibility.Visible;
        PageCountText.Text = workspace.Pages.Count == 1 ? "1 source page" : $"{workspace.Pages.Count} source pages";
        ReviewPages.ItemsSource = workspace.Pages.Select((page, index) =>
            new ReviewPage(page, $"{index + 1:D2}   {page.OriginalName}")).ToArray();
        ReviewPages.SelectedIndex = 0;
    }

    private void ReviewPages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ReviewPreview.Source = (ReviewPages.SelectedItem as ReviewPage)?.Page.Preview;

    private void Transcribe_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null)
            return;

        TranscriptionWindow editor = new(_workspace, ReviewPages.SelectedIndex) { Owner = this };
        editor.ShowDialog();

        if (editor.Saved)
            StatusText.Text = "Transcription saved to the workspace.";
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SettingsWindow settings = new((App)Application.Current) { Owner = this };

        if (settings.ShowDialog() == true)
            StatusText.Text = "Settings saved.";
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        AboutWindow about = new() { Owner = this };
        about.ShowDialog();
    }

    private void OpenWorkFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(_workspace.FolderPath) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            StatusText.Text = $"Could not open {_workspace.FolderPath}";
        }
    }

    private sealed record ReviewPage(SourcePage Page, string Label);
}
