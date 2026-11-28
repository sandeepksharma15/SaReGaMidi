using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SaReGaMidi.UI;

public partial class WorkspacePicker : Window
{
    private readonly string _workFolder;

    internal WorkspacePicker(string workFolder)
    {
        InitializeComponent();
        _workFolder = workFolder;
    }

    internal string? SelectedFolder { get; private set; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            string[] folders = [_workFolder, WorkspaceSettings.Default.WorkFolder];
            IReadOnlyList<WorkspaceListing> entries = await Task.Run(() => folders
                .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(CompositionWorkspace.ListSaved).ToArray());
            Workspaces.ItemsSource = entries;
            if (entries.Count == 0)
                Message.Text = "No saved workspaces found. Browse to another folder.";
            else
                Message.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Message.Text = "Could not list this work folder. Browse to a workspace folder.";
        }
    }

    private void Selection_Changed(object sender, SelectionChangedEventArgs e)
        => OpenButton.IsEnabled = Workspaces.SelectedItem is WorkspaceListing;

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (Workspaces.SelectedItem is WorkspaceListing entry)
        {
            SelectedFolder = entry.Folder;
            DialogResult = true;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new() { Title = "Choose the workspace folder containing composition.json" };
        if (Directory.Exists(_workFolder))
            dialog.InitialDirectory = _workFolder;
        if (dialog.ShowDialog(this) == true)
        {
            SelectedFolder = dialog.FolderName;
            DialogResult = true;
        }
    }
}
