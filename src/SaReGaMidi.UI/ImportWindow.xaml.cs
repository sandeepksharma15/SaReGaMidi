using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SaReGaMidi.UI;

public partial class ImportWindow : Window
{
    #region Fields

    private readonly string _workFolder;
    private readonly List<SourcePage> _pages = [];
    private bool _isBusy;

    #endregion

    #region Constructors

    internal ImportWindow(string workFolder)
    {
        _workFolder = workFolder;
        InitializeComponent();
        RefreshPageList(-1);
        StatusText.Text = "Choose the images for this composition.";
    }

    #endregion

    #region Properties

    internal CompositionWorkspace? Workspace { get; private set; }

    #endregion

    #region Methods

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        e.Cancel |= _isBusy;
    }

    private async void AddPages_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Select composition page images",
            Filter = "Page images|*.jpg;*.jpeg;*.png;*.bmp",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
            await AddPagesAsync(dialog.FileNames);
    }

    private async Task AddPagesAsync(IReadOnlyList<string> filePaths)
    {
        _isBusy = true;
        StatusText.Text = "Loading page previews…";
        UpdateActions();

        List<string> unreadableFiles = [];
        int firstNewIndex = _pages.Count;

        try
        {
            foreach (string filePath in filePaths.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (_pages.Any(page => StringComparer.OrdinalIgnoreCase.Equals(page.FilePath, filePath)))
                    continue;

                try
                {
                    SourcePage page = await Task.Run(() => SourcePage.Load(filePath));
                    _pages.Add(page);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                    or NotSupportedException or ArgumentException or FormatException)
                {
                    unreadableFiles.Add(Path.GetFileName(filePath));
                }
            }
        }
        finally
        {
            _isBusy = false;
            RefreshPageList(_pages.Count > firstNewIndex ? firstNewIndex : PageList.SelectedIndex);
        }

        if (unreadableFiles.Count > 0)
            StatusText.Text = $"Could not open {string.Join(", ", unreadableFiles)}.";
        else if (_pages.Count == firstNewIndex)
            StatusText.Text = "These images are already in the page list.";
    }

    private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PageListItem? selected = PageList.SelectedItem as PageListItem;
        PagePreview.Source = selected?.Page.Preview;
        NoPreviewText.Visibility = selected is null ? Visibility.Visible : Visibility.Collapsed;
        UpdateActions();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
        => MoveSelectedPage(-1);

    private void MoveDown_Click(object sender, RoutedEventArgs e)
        => MoveSelectedPage(1);

    private void MoveSelectedPage(int offset)
    {
        int index = PageList.SelectedIndex;
        int destination = index + offset;

        if (_isBusy || index < 0 || destination < 0 || destination >= _pages.Count)
            return;

        SourcePage page = _pages[index];
        _pages.RemoveAt(index);
        _pages.Insert(destination, page);

        RefreshPageList(destination);
    }

    private void RemovePage_Click(object sender, RoutedEventArgs e)
    {
        int index = PageList.SelectedIndex;

        if (_isBusy || index < 0)
            return;

        _pages.RemoveAt(index);
        RefreshPageList(Math.Min(index, _pages.Count - 1));
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _pages.Count == 0)
            return;

        _isBusy = true;
        StatusText.Text = "Copying scans to the work folder…";
        UpdateActions();

        try
        {
            SourcePage[] pages = _pages.ToArray();
            Workspace = await Task.Run(() => CompositionWorkspace.ImportAsync(_workFolder, pages));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            StatusText.Text = $"Import failed. {exception.Message}";
        }
        finally
        {
            _isBusy = false;
            UpdateActions();
        }

        if (Workspace is not null)
            DialogResult = true;
    }

    private void RefreshPageList(int selectedIndex)
    {
        PageList.ItemsSource = _pages.Select((page, index) =>
            new PageListItem(page, $"{index + 1:D2}   {page.OriginalName}")).ToArray();
        PageList.SelectedIndex = selectedIndex;

        if (PageList.SelectedItem is object selected)
            PageList.ScrollIntoView(selected);

        StatusText.Text = _pages.Count switch
        {
            0 => "No pages selected.",
            1 => "1 page ready to import.",
            _ => $"{_pages.Count} pages ready to import."
        };

        UpdateActions();
    }

    private void UpdateActions()
    {
        int index = PageList.SelectedIndex;

        AddPagesButton.IsEnabled = !_isBusy;
        MoveUpButton.IsEnabled = !_isBusy && index > 0;
        MoveDownButton.IsEnabled = !_isBusy && index >= 0 && index < _pages.Count - 1;
        RemovePageButton.IsEnabled = !_isBusy && index >= 0;
        ImportButton.IsEnabled = !_isBusy && _pages.Count > 0;
        CancelButton.IsEnabled = !_isBusy;
    }

    #endregion

    private sealed record PageListItem(SourcePage Page, string Label);
}
