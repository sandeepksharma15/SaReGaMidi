using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SaReGaMidi.UI;
using Xunit;

namespace SaReGaMidi.Recognition.Tests;

public sealed class CompositionWorkspaceTests
{
    [Fact]
    public async Task Open_SelectedEarlierImport_RestoresItsPagesAndSavedScore()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("SaReGaMidi-workspaces-");
        try
        {
            string image = Path.Combine(root.FullName, "page.png");
            File.WriteAllBytes(image, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/5XkAAAAASUVORK5CYII="));
            SourcePage page = SourcePage.Load(image);
            CompositionWorkspace earlier = await CompositionWorkspace.ImportAsync(root.FullName, [page, page, page]);
            var score = RanjishHiSahiDraft.Create();
            await new ScoreFile(earlier.FolderPath, 3).SaveAsync(score);
            await CompositionWorkspace.ImportAsync(root.FullName, [page]);

            var entries = CompositionWorkspace.ListSaved(root.FullName);
            Assert.Equal(2, entries.Count);
            WorkspaceListing selected = entries.Single(entry => entry.Title == score.Title);
            CompositionWorkspace opened = CompositionWorkspace.Open(selected.Folder);
            Assert.Equal(earlier.FolderPath, opened.FolderPath);
            Assert.Equal(earlier.Pages.Select(source => source.FilePath), opened.Pages.Select(source => source.FilePath));
            Assert.Equal(score.Title, (await new ScoreFile(opened.FolderPath, opened.Pages.Count).LoadAsync())?.Title);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
