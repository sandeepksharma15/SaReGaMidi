using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SaReGaMidi.Music.Rhythm;

namespace SaReGaMidi.UI;

internal sealed class PrintedNotationLayout(BitmapSource source)
{
    private readonly int _width = source.PixelWidth;
    private readonly int _height = source.PixelHeight;
    private readonly byte[] _ink = ReadInk(source);

    internal IReadOnlyList<PrintedLine> Read(Taal taal, CancellationToken cancellationToken)
    {
        List<List<Int32Rect>> groups = [];
        foreach (Int32Rect divider in FindDividers(cancellationToken).OrderBy(CenterY))
        {
            List<Int32Rect>? group = groups.LastOrDefault();
            if (group is null || CenterY(divider) - CenterY(group[0]) > group[0].Height * 0.55)
            {
                group = [];
                groups.Add(group);
            }

            group.Add(divider);
        }

        List<int> distances = [];
        foreach (List<Int32Rect> group in groups)
        {
            group.Sort((left, right) => left.X.CompareTo(right.X));
            for (int index = 1; index < group.Count; index++)
            {
                int distance = group[index].X - group[index - 1].X;
                if (distance >= _width / 12 && distance <= _width / 2)
                    distances.Add(distance);
            }
        }

        if (distances.Count == 0)
            return [];

        int divisionWidth = Median(distances);
        int[] divisionLengths = taal.Vibhaags.Select(division => division.Theka.Count).ToArray();
        List<PrintedLine> lines = [];
        foreach (List<Int32Rect> group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Int32Rect[] bars = group.Where(bar => group.Any(other => other != bar
                && Math.Abs(other.X - bar.X) >= divisionWidth * 0.55
                && Math.Abs(other.X - bar.X) <= divisionWidth * 1.45)).ToArray();
            if (bars.Length < 2)
                continue;

            int length = Median(bars.Select(bar => bar.Height));
            List<PrintedCell> cells = [];
            // A printed row can begin or end with a partial vibhaag.
            for (int division = -1; division < bars.Length; division++)
            {
                int left = division < 0 ? bars[0].X - divisionWidth : bars[division].X;
                bool trailing = division + 1 == bars.Length;
                int right = trailing ? left + divisionWidth * 3 / 2 : bars[division + 1].X;
                int top = division < 0 ? 2 * bars[0].Y - bars[1].Y : bars[division].Y;
                int nextTop = trailing ? 2 * bars[^1].Y - bars[^2].Y : bars[division + 1].Y;
                int count = divisionLengths[(division + divisionLengths.Length) % divisionLengths.Length];
                List<PrintedCell> divisionCells = [];
                int[] boundaries = new int[count + 1];
                for (int beat = 0; beat <= count; beat++)
                {
                    int x = left + (right - left) * beat / count;
                    int y = top + (nextTop - top) * beat / count;
                    boundaries[beat] = trailing && beat > 0 && beat < count
                        ? AlignTrailingBoundary(x, y, length, divisionWidth / (2 * count)) : x;
                }
                for (int beat = 0; beat < count; beat++)
                {
                    int y = top + (nextTop - top) * (2 * beat + 1) / (2 * count);
                    int leftInset = beat == 0 && division >= 0 ? bars[division].Width + 3 : 2;
                    int x1 = Math.Clamp(boundaries[beat] + leftInset, 0, _width);
                    int x2 = Math.Clamp(boundaries[beat + 1] - 2, 0, _width);
                    if (x2 - x1 < 12)
                        continue;

                    Int32Rect swaras = Bounds(x1, y - length / 7, x2, y + length * 43 / 100);
                    Int32Rect lyrics = Bounds(x1, y + length * 43 / 100, x2, y + length + length / 12);
                    divisionCells.Add(new PrintedCell(swaras, lyrics, HasInk(swaras), HasInk(lyrics))
                    {
                        HasSustainStroke = IsSustainStroke(swaras)
                    });
                }

                // A lyric-only return cue can follow a closing divider.
                if (!trailing || bars.Length > 2 || divisionCells.Count(cell => cell.HasSwaras) >= 2)
                    cells.AddRange(divisionCells);
            }

            int first = cells.FindIndex(cell => cell.HasSwaras || cell.HasLyrics);
            int last = cells.FindLastIndex(cell => cell.HasSwaras);
            if (first == 0 && cells.Count > divisionLengths[0] + 1 && bars.Length > 2
                && cells.Skip(1).Take(divisionLengths[0]).All(cell => !cell.HasSwaras && !cell.HasLyrics))
            {
                // Ignore an isolated margin blemish before an otherwise empty pickup.
                first = cells.FindIndex(1, cell => cell.HasSwaras || cell.HasLyrics);
            }

            if (first >= 0 && last >= first)
                lines.Add(new PrintedLine(bars.Min(bar => bar.Y), bars.Max(bar => bar.Y + bar.Height),
                    first % taal.MaatraCount + 1, cells.GetRange(first, last - first + 1)));
        }

        return lines;
    }

    private int AlignTrailingBoundary(int x, int y, int height, int searchWidth)
    {
        // The last vibhaag has no closing divider. Its estimated cut can bisect a note or syllable.
        int top = Math.Max(0, y);
        int bottom = Math.Min(_height, y + height);
        bool Occupied(int column)
        {
            for (int row = top; row < bottom; row++)
                if (_ink[row * _width + column] != 0)
                    return true;
            return false;
        }

        if (x >= _width || x < searchWidth || !Occupied(x))
            return x;

        int gap = 0;
        for (int column = x - 1; column >= x - searchWidth; column--)
        {
            gap = Occupied(column) ? 0 : gap + 1;
            if (gap >= Math.Max(8, height / 10))
                return column + gap / 2;
        }
        return x;
    }

    private List<Int32Rect> FindDividers(CancellationToken cancellationToken)
    {
        byte[] remaining = (byte[])_ink.Clone();
        List<Int32Rect> dividers = [];
        Queue<int> frontier = new();
        for (int start = 0; start < remaining.Length; start++)
        {
            if (start % _width == 0)
                cancellationToken.ThrowIfCancellationRequested();
            if (remaining[start] == 0)
                continue;

            int left = start % _width;
            int right = left;
            int first = start / _width;
            int last = first;
            remaining[start] = 0;
            frontier.Enqueue(start);

            while (frontier.TryDequeue(out int pixel))
            {
                int x = pixel % _width;
                int y = pixel / _width;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                last = Math.Max(last, y);
                for (int neighborY = Math.Max(0, y - 1); neighborY <= Math.Min(_height - 1, y + 1); neighborY++)
                {
                    for (int neighborX = Math.Max(0, x - 1); neighborX <= Math.Min(_width - 1, x + 1); neighborX++)
                    {
                        int neighbor = neighborY * _width + neighborX;
                        if (remaining[neighbor] == 0)
                            continue;
                        remaining[neighbor] = 0;
                        frontier.Enqueue(neighbor);
                    }
                }
            }

            int height = last - first + 1;
            int width = right - left + 1;
            if (height >= _width / 19 && height <= _width / 7 && width * 4 <= height
                && left > _width / 15 && right < _width * 14 / 15)
            {
                dividers.Add(new Int32Rect(left, first, width, height));
            }
        }

        return dividers;
    }

    private Int32Rect Bounds(int left, int top, int right, int bottom)
    {
        top = Math.Clamp(top, 0, _height - 1);
        bottom = Math.Clamp(bottom, top + 1, _height);
        return new Int32Rect(left, top, right - left, bottom - top);
    }

    private bool HasInk(Int32Rect rect)
    {
        int count = 0;
        for (int y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            for (int x = rect.X; x < rect.X + rect.Width; x++)
                count += _ink[y * _width + x];
        }

        return count >= 30;
    }

    internal bool IsSustainStroke(Int32Rect rect)
    {
        int left = rect.X + rect.Width;
        int right = rect.X;
        int top = rect.Y + rect.Height;
        int bottom = rect.Y;
        int area = 0;
        for (int y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            for (int x = rect.X; x < rect.X + rect.Width; x++)
            {
                if (_ink[y * _width + x] == 0)
                    continue;

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
                area++;
            }
        }

        int width = right - left + 1;
        int height = bottom - top + 1;
        return area >= 30 && width >= rect.Width / 12 && height <= rect.Height / 7
            && width >= height * 3 && area >= width * height * 0.34;
    }

    private static byte[] ReadInk(BitmapSource source)
    {
        BitmapSource gray = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
        byte[] pixels = new byte[gray.PixelWidth * gray.PixelHeight];
        gray.CopyPixels(pixels, gray.PixelWidth, 0);
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = pixels[index] < 110 ? (byte)1 : (byte)0;
        return pixels;
    }

    private static int CenterY(Int32Rect rect) => rect.Y + rect.Height / 2;

    private static int Median(IEnumerable<int> values)
    {
        int[] sorted = values.Order().ToArray();
        return sorted[sorted.Length / 2];
    }
}

internal sealed record PrintedLine(int Top, int Bottom, int StartBeat, IReadOnlyList<PrintedCell> Cells);

internal sealed record PrintedCell(Int32Rect Swaras, Int32Rect Lyrics, bool HasSwaras, bool HasLyrics)
{
    internal bool HasSustainStroke { get; init; }
}
