using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace SaReGaMidi.UI;

public partial class SettingsWindow : Window
{
    private readonly App _application;

    internal SettingsWindow(App application)
    {
        InitializeComponent();
        _application = application;

        WorkFolderBox.Text = application.Settings.WorkFolder;
        ThemeBox.SelectedIndex = application.Settings.Theme == AppTheme.Dark ? 1 : 0;
        StartupBox.SelectedIndex = application.Settings.StartWithPreviousWorkspace ? 0 : 1;
        StatusText.Text = "Folder changes apply to future imports. Startup changes apply next launch.";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new() { Title = "Choose a SaReGaMidi work folder" };

        if (Directory.Exists(WorkFolderBox.Text))
            dialog.InitialDirectory = WorkFolderBox.Text;

        if (dialog.ShowDialog(this) == true)
            WorkFolderBox.Text = dialog.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string workFolder = Environment.ExpandEnvironmentVariables(WorkFolderBox.Text.Trim());

        if (!Path.IsPathFullyQualified(workFolder))
        {
            StatusText.Text = "Choose an absolute folder path.";
            WorkFolderBox.Focus();
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(workFolder);
            Directory.CreateDirectory(fullPath);
            AppTheme theme = ThemeBox.SelectedIndex == 1 ? AppTheme.Dark : AppTheme.Light;
            _application.UpdateSettings(_application.Settings with
            {
                WorkFolder = fullPath,
                Theme = theme,
                StartWithPreviousWorkspace = StartupBox.SelectedIndex == 0
            });
            DialogResult = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            StatusText.Text = "Could not save settings. Check the work folder path and permissions.";
        }
    }
}
