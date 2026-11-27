using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace SaReGaMidi.UI;

public partial class App : Application
{
    internal WorkspaceSettings Settings { get; private set; } = WorkspaceSettings.Default;

    internal string? StartupMessage { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            Settings = WorkspaceSettings.Load(WorkspaceSettings.SettingsPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            StartupMessage = "Settings could not be loaded. Using the default work folder and light theme.";
        }

        ApplyTheme(Settings.Theme);
        base.OnStartup(e);
    }

    internal void UpdateSettings(WorkspaceSettings settings)
    {
        settings.Save(WorkspaceSettings.SettingsPath);
        Settings = settings;
        ApplyTheme(settings.Theme);
    }

    internal void RememberWorkspace(string folderPath)
    {
        Settings = Settings with { PreviousWorkspace = folderPath };
        Settings.Save(WorkspaceSettings.SettingsPath);
    }

    private void ApplyTheme(AppTheme theme)
    {
        bool dark = theme == AppTheme.Dark;

#pragma warning disable WPF0001
        ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
#pragma warning restore WPF0001

        Resources["CanvasBrush"] = CreateBrush(dark ? "#171B20" : "#F3F5F8");
        Resources["CardBrush"] = CreateBrush(dark ? "#222830" : "#FFFFFF");
        Resources["LineBrush"] = CreateBrush(dark ? "#39424E" : "#DDE3EB");
        Resources["InkBrush"] = CreateBrush(dark ? "#F1F4F7" : "#172333");
        Resources["MutedBrush"] = CreateBrush(dark ? "#B5C0CC" : "#506076");
        Resources["SelectionBrush"] = CreateBrush(dark ? "#304B49" : "#D9EEEA");
        Resources["AccentBrush"] = CreateBrush(dark ? "#8BD9CC" : "#08665D");
        Resources["OnAccentBrush"] = CreateBrush(dark ? "#122D29" : "#FFFFFF");
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
