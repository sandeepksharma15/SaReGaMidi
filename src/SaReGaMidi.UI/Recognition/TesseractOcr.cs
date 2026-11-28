using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Exceptions;
using OcrImage = TesseractOCR.Pix.Image;

namespace SaReGaMidi.UI;

internal static class TesseractOcr
{
    internal static Task<OcrReading[]> ReadAsync(BitmapSource image, IReadOnlyList<Int32Rect> crops,
        CancellationToken cancellationToken)
    {
        string? executable = Environment.GetEnvironmentVariable("SAREGAMIDI_TESSERACT");
        if (!string.IsNullOrWhiteSpace(executable))
            return ReadExecutableAsync(image, crops, Path.GetFullPath(executable.Trim()), cancellationToken);

        return Task.FromResult(ReadInProcess(image, crops, cancellationToken));
    }

    private static OcrReading[] ReadInProcess(BitmapSource image, IReadOnlyList<Int32Rect> crops,
        CancellationToken cancellationToken)
    {
        if (crops.Count == 0)
            return [];

        cancellationToken.ThrowIfCancellationRequested();
        using Engine engine = CreateEngine();
        OcrReading[] readings = new OcrReading[crops.Count];
        for (int index = 0; index < crops.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using MemoryStream png = new();
            WriteCrop(image, crops[index], png);
            using OcrImage crop = OcrImage.LoadFromMemory(png.ToArray());
            using Page page = engine.Process(crop, PageSegMode.SingleLine);
            readings[index] = ParseReadings(page.TsvText, 1, hasHeader: false)[0];
        }
        cancellationToken.ThrowIfCancellationRequested();
        return readings;
    }

    private static Engine CreateEngine()
    {
        string data = FindDataDirectory();
        try
        {
            return new Engine(data, "hin", EngineMode.Default);
        }
        catch (Exception exception) when (exception is DllNotFoundException or TypeInitializationException
            or BadImageFormatException or EntryPointNotFoundException or LoadLibraryException)
        {
            throw new InvalidOperationException("Tesseract's native libraries could not load. "
                + "Check that the app has its OCR DLLs and the matching Microsoft Visual C++ Redistributable.", exception);
        }
        catch (TesseractException exception)
        {
            throw new InvalidOperationException("Tesseract could not initialize the bundled Hindi OCR model.", exception);
        }
    }

    private static async Task<OcrReading[]> ReadExecutableAsync(BitmapSource image, IReadOnlyList<Int32Rect> crops,
        string executable, CancellationToken cancellationToken)
    {
        if (crops.Count == 0)
            return [];

        DirectoryInfo scratch = Directory.CreateTempSubdirectory("SaReGaMidi-Ocr-");
        try
        {
            string[] inputs = new string[crops.Count];
            for (int index = 0; index < crops.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                inputs[index] = $"crop-{index:D4}.png";
                using FileStream output = File.Create(Path.Combine(scratch.FullName, inputs[index]));
                WriteCrop(image, crops[index], output);
            }

            File.WriteAllLines(Path.Combine(scratch.FullName, "crops.txt"), inputs);
            ProcessStartInfo start = new()
            {
                FileName = executable,
                WorkingDirectory = scratch.FullName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string argument in new[] { "crops.txt", "stdout", "--tessdata-dir", FindDataDirectory(),
                "-l", "hin", "--psm", "7", "-c", "tessedit_create_tsv=1" })
                start.ArgumentList.Add(argument);

            using Process process = new() { StartInfo = start };
            try
            {
                process.Start();
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
            {
                throw new InvalidOperationException("SAREGAMIDI_TESSERACT must point to a valid Tesseract executable. "
                    + "Clear it to use the bundled OCR engine.", exception);
            }

            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }

                await process.WaitForExitAsync().ConfigureAwait(false);
                throw;
            }

            string error = await stderr.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                string message = error.Contains("hin.traineddata", StringComparison.OrdinalIgnoreCase)
                    ? "Tesseract could not load the bundled Hindi OCR model."
                    : $"Tesseract could not read the scan crops (exit code {process.ExitCode}).";
                throw new InvalidOperationException(message);
            }

            return ParseReadings(await stdout.ConfigureAwait(false), crops.Count);
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    private static string FindDataDirectory()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (!File.Exists(Path.Combine(directory, "hin.traineddata")))
            throw new InvalidOperationException("The app is missing tessdata/hin.traineddata. Rebuild or reinstall SaReGaMidi.");
        return directory;
    }

    private static void WriteCrop(BitmapSource image, Int32Rect bounds, Stream output)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(CreateCrop(image, bounds)));
        encoder.Save(output);
    }

    private static BitmapSource CreateCrop(BitmapSource image, Int32Rect bounds)
    {
        CroppedBitmap crop = new(image, bounds);
        if (bounds.Height >= 66)
            return crop;

        const int margin = 12;
        int width = bounds.Width * 2 + margin * 2;
        int height = bounds.Height * 2 + margin * 2;
        byte[] padded = new byte[width * height];
        Array.Fill(padded, (byte)255);
        byte[] pixels = new byte[bounds.Width * bounds.Height];
        new FormatConvertedBitmap(crop, PixelFormats.Gray8, null, 0).CopyPixels(pixels, bounds.Width, 0);
        for (int y = 0; y < bounds.Height * 2; y++)
        {
            for (int x = 0; x < bounds.Width * 2; x++)
                padded[(y + margin) * width + x + margin] = pixels[(y / 2) * bounds.Width + x / 2];
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, padded, width);
    }

    internal static OcrReading[] ParseReadings(string tsv, int count, bool hasHeader = true)
    {
        List<string>[] words = Enumerable.Range(0, count).Select(_ => new List<string>()).ToArray();
        double[] confidence = Enumerable.Repeat(100d, count).ToArray();
        using StringReader input = new(tsv);
        if (hasHeader && input.ReadLine()?.StartsWith("level\tpage_num\t", StringComparison.Ordinal) != true)
            throw new InvalidDataException("Tesseract returned invalid OCR output.");

        while (input.ReadLine() is string line)
        {
            string[] columns = line.Split('\t', 12);
            if (columns.Length != 12 || columns[0] != "5" || string.IsNullOrWhiteSpace(columns[11]))
                continue;

            // In-process OCR yields one headerless result per crop, regardless of the native page number.
            int page = 1;
            if (hasHeader && (!int.TryParse(columns[1], NumberStyles.None, CultureInfo.InvariantCulture, out page)
                || page < 1 || page > count))
                continue;

            words[page - 1].Add(columns[11].Trim());
            if (!double.TryParse(columns[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double score))
                score = 0;
            confidence[page - 1] = Math.Min(confidence[page - 1], score);
        }

        return words.Select((line, index) => new OcrReading(string.Join(" ", line),
            line.Count == 0 ? 0 : confidence[index])).ToArray();
    }
}

internal readonly record struct OcrReading(string Text, double Confidence);
