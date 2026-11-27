using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SaReGaMidi.UI;

internal sealed record CompositionWorkspace(string FolderPath, IReadOnlyList<SourcePage> Pages)
{
    internal static async Task<CompositionWorkspace> ImportAsync(string workFolder, IReadOnlyList<SourcePage> pages)
    {
        Directory.CreateDirectory(workFolder);

        string importId = Guid.NewGuid().ToString("N");
        string workspacePath = Path.Combine(workFolder, $"Composition-{DateTime.Now:yyyyMMdd-HHmmss}-{importId[..8]}");
        string stagingPath = Path.Combine(workFolder, $".import-{importId}");

        try
        {
            string sourcePath = Path.Combine(stagingPath, "source");
            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(Path.Combine(stagingPath, "artifacts"));

            List<SourcePage> importedPages = new(pages.Count);
            List<ManifestPage> manifestPages = new(pages.Count);

            for (int index = 0; index < pages.Count; index++)
            {
                SourcePage page = pages[index];
                int number = index + 1;
                string originalName = page.OriginalName;
                string storedName = $"{number:D3}{Path.GetExtension(originalName)}";

                await using (FileStream input = new(page.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 81920, useAsync: true))
                await using (FileStream output = new(Path.Combine(sourcePath, storedName), FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
                {
                    await input.CopyToAsync(output).ConfigureAwait(false);
                }

                importedPages.Add(page with { FilePath = Path.Combine(workspacePath, "source", storedName) });
                manifestPages.Add(new ManifestPage(number, $"source/{storedName}", originalName));
            }

            await using (FileStream manifest = File.Create(Path.Combine(stagingPath, "composition.json")))
            {
                await JsonSerializer.SerializeAsync(manifest, new Manifest(1, manifestPages),
                    new JsonSerializerOptions { WriteIndented = true }).ConfigureAwait(false);
            }

            Directory.Move(stagingPath, workspacePath);

            return new CompositionWorkspace(workspacePath, importedPages.AsReadOnly());
        }
        finally
        {
            if (Directory.Exists(stagingPath))
            {
                try
                {
                    Directory.Delete(stagingPath, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Preserve the import error if the incomplete staging folder cannot be removed.
                }
            }
        }
    }

    internal static CompositionWorkspace? Reopen(string workFolder, string? previousWorkspace)
    {
        string? folderPath = previousWorkspace ?? FindLatestImport(workFolder);
        if (folderPath is null)
            return null;

        if (!Path.IsPathFullyQualified(folderPath))
            throw new InvalidDataException("The previous workspace path is invalid.");

        using FileStream manifestFile = File.OpenRead(Path.Combine(folderPath, "composition.json"));
        Manifest? manifest = JsonSerializer.Deserialize<Manifest>(manifestFile);
        if (manifest is not { Version: 1, Pages.Count: > 0 })
            throw new InvalidDataException("The workspace manifest is empty or uses an unsupported version.");

        List<SourcePage> pages = new(manifest.Pages.Count);
        for (int index = 0; index < manifest.Pages.Count; index++)
        {
            ManifestPage entry = manifest.Pages[index];
            int number = index + 1;
            if (entry is null || entry.Number != number || string.IsNullOrWhiteSpace(entry.OriginalName)
                || entry.OriginalName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException("The workspace manifest contains an invalid page entry.");
            }

            string fileName = $"{number:D3}{Path.GetExtension(entry.OriginalName)}";
            if (!string.Equals(entry.Path, $"source/{fileName}", StringComparison.Ordinal))
                throw new InvalidDataException("The workspace manifest contains an invalid source path.");

            SourcePage page = SourcePage.Load(Path.Combine(folderPath, "source", fileName));
            pages.Add(page with { OriginalName = entry.OriginalName });
        }

        return new CompositionWorkspace(folderPath, pages.AsReadOnly());
    }

    private static string? FindLatestImport(string workFolder)
    {
        if (!Directory.Exists(workFolder))
            return null;

        return Directory.EnumerateDirectories(workFolder, "Composition-*")
            .Where(folder => File.Exists(Path.Combine(folder, "composition.json")))
            .OrderByDescending(Directory.GetCreationTimeUtc)
            .ThenByDescending(Path.GetFileName, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private sealed record Manifest(int Version, IReadOnlyList<ManifestPage> Pages);

    private sealed record ManifestPage(int Number, string Path, string OriginalName);
}
