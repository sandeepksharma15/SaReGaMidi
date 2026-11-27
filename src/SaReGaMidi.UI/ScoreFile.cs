using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SaReGaMidi.Music.Rhythm;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

internal sealed class ScoreFile(string folderPath, int pageCount)
{
    private readonly string _path = Path.Combine(folderPath, "score.json");

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    internal async Task<CompositionScore?> LoadAsync()
    {
        try
        {
            await using FileStream input = File.OpenRead(_path);
            Document? document = await JsonSerializer.DeserializeAsync<Document>(input, _options).ConfigureAwait(false);
            if (document is not { Version: 1, Score: not null })
                throw new InvalidDataException("The score file uses an unsupported format.");

            Validate(document.Score);
            return document.Score;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    internal static string? ReadTitle(string folder)
    {
        try
        {
            using FileStream stream = File.OpenRead(Path.Combine(folder, "score.json"));
            using JsonDocument document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("Score", out JsonElement score) && score.ValueKind == JsonValueKind.Object
                && score.TryGetProperty("Title", out JsonElement title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Source pages can still be opened without a readable score.
        }
        return null;
    }

    internal async Task SaveAsync(CompositionScore score)
    {
        Validate(score);
        string temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (FileStream output = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(output, new Document(1, score), _options).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private void Validate(CompositionScore score)
    {
        if (string.IsNullOrWhiteSpace(score.Title) || string.IsNullOrWhiteSpace(score.TaalName)
            || TaalCatalog.FindByName(score.TaalName) is not Taal taal || score.Sections is not { Count: > 0 })
        {
            throw new InvalidDataException("Enter a title, choose a taal, and enter at least one maatra.");
        }

        foreach (ScoreSection section in score.Sections)
        {
            if (section is null || string.IsNullOrWhiteSpace(section.Name) || section.StartBeat < 1
                || section.StartBeat > taal.MaatraCount || section.Maatras is not { Count: > 0 })
            {
                throw new InvalidDataException($"Each section needs a name, a start beat from 1 to {taal.MaatraCount}, and at least one maatra.");
            }

            foreach (ScoreMaatra maatra in section.Maatras)
            {
                if (maatra is null || maatra.SourcePage < 1 || maatra.SourcePage > pageCount
                    || maatra.Swaras is null || maatra.Lyrics is null || maatra.Annotation is null)
                {
                    throw new InvalidDataException($"Each maatra needs a valid reference to source pages 1 to {pageCount}.");
                }

                if (maatra.Swaras.Count > 0)
                    MaatraNotation.Validate(maatra.Swaras);
            }
        }

        ScoreMaatra[] written = score.Sections.SelectMany(section => section.Maatras).ToArray();
        int maatraCount = written.Length;
        Dictionary<Guid, int> anchors = [];
        List<(Guid Target, int Position)> meends = [];
        int position = 0;

        for (int index = 0; index < maatraCount; index++)
        {
            ScoreMaatra maatra = written[index];
            if (maatra.BorrowedFrom is int source && (maatra.Swaras.Count != 0
                || source < 0 || source >= index || written[source].Swaras.Count == 0))
            {
                throw new InvalidDataException($"Maatra {index + 1} must borrow from an earlier written maatra with swaras.");
            }

            foreach (SwaraEvent swara in maatra.Swaras)
            {
                if (swara.AnchorId is Guid anchor && !anchors.TryAdd(anchor, position))
                    throw new InvalidDataException($"Maatra {index + 1} has a duplicate meend anchor.");

                if (swara.MeendTargetId is Guid target)
                    meends.Add((target, position));

                position++;
            }
        }

        if (meends.Any(link => !anchors.TryGetValue(link.Target, out int end) || end <= link.Position))
            throw new InvalidDataException("A meend must end on a later written swara. Repair or clear the link before saving.");
        if (score.Passes is null || score.Passes.Any(pass => pass is null
            || string.IsNullOrWhiteSpace(pass.Name) || pass.StartMaatra < 0
            || pass.MaatraCount <= 0 || pass.StartMaatra > maatraCount - pass.MaatraCount))
        {
            throw new InvalidDataException("A repeat pass points outside the written score.");
        }
    }

    private sealed record Document(int Version, CompositionScore Score);
}
