using System.IO;
using System.Windows.Media.Imaging;

namespace SaReGaMidi.UI;

internal sealed record SourcePage(string FilePath, string OriginalName, BitmapSource Preview)
{
    internal static SourcePage Load(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        BitmapImage preview = new();

        preview.BeginInit();
        preview.CacheOption = BitmapCacheOption.OnLoad;
        preview.DecodePixelWidth = 1800;
        preview.StreamSource = stream;
        preview.EndInit();
        preview.Freeze();

        return new SourcePage(filePath, Path.GetFileName(filePath), preview);
    }
}
