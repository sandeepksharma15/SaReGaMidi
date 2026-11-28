using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaReGaMidi.UI;

internal sealed record WorkspaceSettings(string WorkFolder, AppTheme Theme)
{
    private static readonly string _settingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaReGaMidi");
    internal static string SettingsPath { get; } = Path.Combine(_settingsDirectory, "settings.json");

    internal static WorkspaceSettings Default { get; } = new(
        Path.Combine(_settingsDirectory, "Work"), AppTheme.Light);

    public bool StartWithPreviousWorkspace { get; init; } = true;

    public string? PreviousWorkspace { get; init; }

    internal static WorkspaceSettings Load(string settingsPath)
    {
        try
        {
            using FileStream stream = File.OpenRead(settingsPath);
            WorkspaceSettings? settings = JsonSerializer.Deserialize<WorkspaceSettings>(stream);

            if (settings is null || string.IsNullOrWhiteSpace(settings.WorkFolder)
                || !Path.IsPathFullyQualified(settings.WorkFolder) || !Enum.IsDefined(settings.Theme))
            {
                throw new JsonException("The settings file contains an invalid work folder or theme.");
            }

            return settings;
        }
        catch (FileNotFoundException)
        {
            return Default;
        }
        catch (DirectoryNotFoundException)
        {
            return Default;
        }
    }

    internal void Save(string settingsPath)
    {
        string settingsDirectory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(settingsDirectory);

        string temporaryPath = Path.Combine(settingsDirectory, $"settings-{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = File.Create(temporaryPath))
                JsonSerializer.Serialize(stream, this);

            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
internal enum AppTheme
{
    Light,
    Dark
}
