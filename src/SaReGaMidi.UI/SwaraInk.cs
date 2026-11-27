using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SaReGaMidi.UI;

internal sealed class SwaraInk
{
    private readonly int _width;
    private readonly int _height;
    private readonly byte[] _gray;

    internal SwaraInk(BitmapSource image)
    {
        _width = image.PixelWidth;
        _height = image.PixelHeight;
        _gray = new byte[_width * _height];
        new FormatConvertedBitmap(image, PixelFormats.Gray8, null, 0).CopyPixels(_gray, _width, 0);
    }

    internal SwaraMarks Read(Int32Rect crop)
    {
        List<InkShape> shapes = Components(crop);
        InkShape[] bodies = shapes.Where(shape => shape.Bounds.Height >= crop.Height / 4
            && shape.Area >= 130).OrderBy(shape => shape.Bounds.X).ToArray();
        InkShape[] strokes = shapes.Where(shape => shape.Bounds.Height >= 6 && shape.Bounds.Height <= 15
            && shape.Bounds.Height >= shape.Bounds.Width * 1.5 && shape.Area >= 16
            && bodies.Any(body => CenterX(shape.Bounds) >= body.Bounds.X && CenterX(shape.Bounds) <= Right(body.Bounds) + 2
                && body.Bounds.Y - Bottom(shape.Bounds) is >= -2 and <= 8)).ToArray();
        bool tivra = strokes.Length > 0 || bodies.Any(body => HasJoinedTivra(body.Bounds));
        InkShape[] substantial = shapes.Where(shape => shape.Area >= 35).ToArray();
        string? symbol = substantial.Length == 1 ? ReadIsolatedSymbol(substantial[0], crop) : null;
        InkShape[] baselineBodies = shapes.Where(shape => shape.Bounds.Height >= crop.Height / 5 && shape.Area >= 130
            && shape.Bounds.Y >= crop.Y + crop.Height / 9).ToArray();
        Int32Rect? fallback = baselineBodies is [InkShape fallbackBody] ? Pad(fallbackBody.Bounds, crop) : null;
        char? octave = bodies is [InkShape body] ? ReadOctave(body.Bounds, shapes) : null;
        if (bodies.Length != 2)
            return new(tivra, fallback, null, null, octave, null, null, symbol);

        Int32Rect kan = bodies[0].Bounds;
        Int32Rect main = bodies[1].Bounds;
        char? kanOctave = ReadOctave(kan, shapes);
        char? mainOctave = ReadOctave(main, shapes);
        int rise = main.Y - kan.Y;
        bool hasKan = Right(kan) <= main.X + 4 && Right(kan) >= main.X - main.Width / 2
            && Bottom(kan) + main.Height / 3 < Bottom(main)
            && (rise >= main.Height * 0.70 || (rise >= main.Height / 4 && kanOctave == '.' && mainOctave == '.'));
        return new(tivra, fallback, hasKan ? Pad(kan, crop) : null, hasKan ? Pad(main, crop) : null,
            octave, hasKan ? kanOctave : null, hasKan ? mainOctave : null, symbol);
    }

    private bool HasJoinedTivra(Int32Rect body)
    {
        int headline = body.Y;
        int widestRow = 0;
        for (int y = body.Y; y < body.Y + body.Height / 3; y++)
        {
            int width = 0;
            for (int x = body.X; x < Right(body); x++)
                if (IsInk(x, y))
                    width++;

            if (width > widestRow)
            {
                widestRow = width;
                headline = y;
            }
        }

        if (headline - body.Y < 7 || widestRow < body.Width * 0.55)
            return false;

        // Read the narrow stroke above the headline even when the ink touches it.
        Int32Rect above = new(body.X, body.Y, body.Width, headline - body.Y - 1);
        return Components(above).Any(shape => shape.Bounds.Height is >= 6 and <= 15
            && shape.Bounds.Height >= shape.Bounds.Width * 1.5 && shape.Area >= 16
            && headline - Bottom(shape.Bounds) <= 4);
    }

    private char? ReadOctave(Int32Rect body, IEnumerable<InkShape> shapes)
    {
        if (HasLowerDot(body, shapes))
            return '.';

        foreach (InkShape shape in shapes)
        {
            Int32Rect dot = shape.Bounds;
            if (!IsDot(dot) || dot.Height > dot.Width * 1.4 || CenterX(dot) < CenterX(body) || Right(dot) > Right(body))
                continue;

            int headline = TopInk(body, Math.Max(body.X, dot.X - 2), Math.Min(Right(body), Right(dot) + 2));
            if (headline - Bottom(dot) is >= 2 and <= 13 && headline < body.Y + body.Height / 3)
                return '\'';
        }
        return null;
    }

    internal LyricMarks ReadLyricMarks(Int32Rect crop)
    {
        InkShape[] glyphs = Components(crop).Where(shape => shape.Area >= 30
            && shape.Bounds.Height >= Math.Max(4, crop.Height / 10))
            .OrderBy(shape => shape.Bounds.X).ToArray();
        int first = 0;
        int last = glyphs.Length;
        int leading = 0;
        int trailing = 0;
        // Read a standalone run as trailing so it has the same representation as the OCR fallback.
        while (last > first)
        {
            int count = CountContinuationMarks(glyphs[last - 1].Bounds);
            if (count == 0)
                break;
            trailing += count;
            last--;
        }
        while (first < last)
        {
            int count = CountContinuationMarks(glyphs[first].Bounds);
            if (count == 0)
                break;
            leading += count;
            first++;
        }

        if (first == last)
            return new(leading, trailing, null);

        int left = first == 0 ? crop.X : glyphs[first].Bounds.X;
        int right = last == glyphs.Length ? Right(crop) : Right(glyphs[last - 1].Bounds);
        if (first > 0)
            left = Math.Max(left - 2, Right(glyphs[first - 1].Bounds));
        Int32Rect firstGlyph = glyphs[first].Bounds;
        if (FindJoinedLeadingMark(firstGlyph) is int joinedEnd)
        {
            leading++;
            left = joinedEnd;
        }
        if (leading + trailing == 0)
            return default;
        if (last < glyphs.Length)
            right = Math.Min(right + 2, glyphs[last].Bounds.X);
        return right > left ? new(leading, trailing, new Int32Rect(left, crop.Y, right - left, crop.Height)) : default;
    }

    internal Int32Rect Tighten(Int32Rect crop, out bool excludesDistantInk)
    {
        InkShape[] shapes = Components(crop).Where(shape => shape.Area >= 16).ToArray();
        excludesDistantInk = false;
        if (shapes.Length == 0)
            return crop;

        Int32Rect main = shapes.MaxBy(shape => shape.Area)!.Bounds;
        InkShape[] nearby = shapes.Where(shape => shape.Bounds.X - Right(main) < main.Height
            && main.X - Right(shape.Bounds) < main.Height).ToArray();
        excludesDistantInk = shapes.Any(shape => shape.Bounds.X - Right(main) >= main.Height * 1.5
            || main.X - Right(shape.Bounds) >= main.Height * 1.5);
        int left = nearby.Min(shape => shape.Bounds.X);
        int top = nearby.Min(shape => shape.Bounds.Y);
        int right = nearby.Max(shape => Right(shape.Bounds));
        int bottom = nearby.Max(shape => Bottom(shape.Bounds));
        return Pad(new Int32Rect(left, top, right - left, bottom - top), crop);
    }

    internal bool HasLyricNukta(Int32Rect crop)
    {
        List<InkShape> shapes = Components(crop);
        InkShape? body = shapes.MaxBy(shape => shape.Area);
        return body is not null && HasLowerDot(body.Bounds, shapes);
    }

    internal bool IsLyricSilence(Int32Rect crop)
    {
        InkShape[] shapes = Components(crop).Where(shape => shape.Area >= 35).ToArray();
        return shapes is [InkShape shape] && ReadIsolatedSymbol(shape, crop) == "*";
    }

    private int? FindJoinedLeadingMark(Int32Rect bounds)
    {
        // A leading sign can touch the syllable's headline near the top of the glyph.
        for (int height = bounds.Height; height >= bounds.Height * 0.65; height--)
        {
            int width = (int)Math.Round(height * 0.66);
            if (bounds.Width < width * 2)
                continue;

            Int32Rect candidate = new(bounds.X, bounds.Y, width, height);
            if (CountContinuationMarks(candidate) == 1 && IsInk(Right(candidate), bounds.Y + height / 10))
                return Right(candidate);
        }
        return null;
    }

    private int CountContinuationMarks(Int32Rect bounds)
    {
        double aspect = (double)bounds.Width / bounds.Height;
        if (aspect is < 0.51 or > 1.52)
            return 0;

        // The printed sign has a left hook, a descending diagonal and a curl back to the left.
        // Adjacent signs may share ink at the curl, so sample the two strokes above it.
        List<(double Left, double Right)> upper = InkRuns(bounds, 0.25);
        List<(double Left, double Right)> middle = InkRuns(bounds, 0.45);
        List<(double Left, double Right)> lower = InkRuns(bounds, 0.65);
        List<(double Left, double Right)> curl = InkRuns(bounds, 0.95);
        if (aspect <= 0.81 && upper is [(<= 0.21, <= 0.46)] && middle is [(>= 0.17 and <= 0.50, >= 0.48 and <= 0.75)]
            && lower is [(>= 0.50 and <= 0.82, >= 0.76)] && curl.Count > 0 && curl[0].Left <= 0.45
            && curl[^1].Right <= 0.86)
            return 1;

        if (aspect >= 1.08 && upper is [(<= 0.13, <= 0.25), (>= 0.43 and <= 0.62, <= 0.76)]
            && middle is [(<= 0.25, <= 0.37), (>= 0.57 and <= 0.72, >= 0.70 and <= 0.90)]
            && lower is [(>= 0.22 and <= 0.42, <= 0.58), (>= 0.77, >= 0.90)]
            && curl.Count > 0 && curl[0].Left <= 0.25 && curl[^1].Right <= 0.94)
            return 2;

        return 0;
    }

    private List<(double Left, double Right)> InkRuns(Int32Rect bounds, double row)
    {
        List<(double Left, double Right)> runs = [];
        int y = bounds.Y + (int)(bounds.Height * row);
        int start = -1;
        for (int x = 0; x <= bounds.Width; x++)
        {
            if (x < bounds.Width && IsInk(bounds.X + x, y))
            {
                if (start < 0)
                    start = x;
            }
            else if (start >= 0)
            {
                runs.Add(((double)start / bounds.Width, (double)x / bounds.Width));
                start = -1;
            }
        }
        return runs;
    }

    private string? ReadIsolatedSymbol(InkShape shape, Int32Rect crop)
    {
        Int32Rect bounds = shape.Bounds;
        if (bounds.Height <= crop.Height / 8 && bounds.Width >= bounds.Height * 4
            && shape.Area >= bounds.Width * bounds.Height * 0.32)
            return "-";

        int middleX = CenterX(bounds);
        int middleY = bounds.Y + bounds.Height / 2;
        if (bounds.Height >= crop.Height / 8 && bounds.Height <= crop.Height / 4
            && bounds.Width >= bounds.Height * 0.78 && bounds.Width <= bounds.Height * 1.25
            && shape.Area >= bounds.Width * bounds.Height * 0.30 && shape.Area <= bounds.Width * bounds.Height * 0.60
            && IsInk(middleX, middleY) && !IsInk(bounds.X + 1, bounds.Y + 1)
            && !IsInk(Right(bounds) - 2, Bottom(bounds) - 2))
            return "*";

        return null;
    }

    internal int? FindMeendBottom(Int32Rect left, Int32Rect right)
    {
        int x = left.X + left.Width / 4;
        int y = Math.Max(0, Math.Min(left.Y, right.Y) - left.Height / 6);
        int end = right.X + right.Width * 3 / 4;
        int bottom = Math.Min(Bottom(left), Bottom(right)) - Math.Min(left.Height, right.Height) / 2;
        if (bottom <= y)
            return null;

        InkShape? arc = Components(new Int32Rect(x, y, end - x, bottom - y)).FirstOrDefault(shape =>
            shape.Bounds.X < Right(left) - 10 && Right(shape.Bounds) > right.X + 10
            && shape.Bounds.Width >= Math.Min(left.Width, right.Width) * 0.56
            && shape.Bounds.Height >= 12 && shape.Bounds.Height <= left.Height * 0.42
            && shape.Area < shape.Bounds.Width * shape.Bounds.Height * 0.55
            && IsArch(shape.Bounds));
        return arc is null ? null : Bottom(arc.Bounds);
    }

    private bool IsArch(Int32Rect bounds)
    {
        int left = TopInk(bounds, bounds.X, bounds.X + bounds.Width / 5);
        int center = TopInk(bounds, bounds.X + bounds.Width * 2 / 5, bounds.X + bounds.Width * 3 / 5);
        int right = TopInk(bounds, Right(bounds) - bounds.Width / 5, Right(bounds));
        return left >= center + 4 && right >= center + 4;
    }

    private int TopInk(Int32Rect bounds, int left, int right)
    {
        for (int y = bounds.Y; y < Bottom(bounds); y++)
        {
            for (int x = left; x < right; x++)
                if (IsInk(x, y))
                    return y;
        }

        return Bottom(bounds);
    }

    private List<InkShape> Components(Int32Rect rect)
    {
        bool[] seen = new bool[rect.Width * rect.Height];
        List<InkShape> components = [];
        Queue<int> pending = new();
        for (int index = 0; index < seen.Length; index++)
        {
            int left = index % rect.Width;
            int top = index / rect.Width;
            if (seen[index] || !IsInk(rect.X + left, rect.Y + top))
                continue;

            seen[index] = true;
            pending.Enqueue(index);
            int right = left;
            int bottom = top;
            int area = 0;
            while (pending.TryDequeue(out int pixel))
            {
                int x = pixel % rect.Width;
                int y = pixel / rect.Width;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
                area++;
                for (int nearY = Math.Max(0, y - 1); nearY <= Math.Min(rect.Height - 1, y + 1); nearY++)
                {
                    for (int nearX = Math.Max(0, x - 1); nearX <= Math.Min(rect.Width - 1, x + 1); nearX++)
                    {
                        int neighbor = nearY * rect.Width + nearX;
                        if (seen[neighbor] || !IsInk(rect.X + nearX, rect.Y + nearY))
                            continue;

                        seen[neighbor] = true;
                        pending.Enqueue(neighbor);
                    }
                }
            }

            if (area >= 6)
                components.Add(new(new Int32Rect(rect.X + left, rect.Y + top, right - left + 1, bottom - top + 1), area));
        }

        return components;
    }

    private bool IsInk(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _height && _gray[y * _width + x] < 135;

    private static bool HasLowerDot(Int32Rect body, IEnumerable<InkShape> shapes)
        => shapes.Any(shape => IsDot(shape.Bounds)
            && shape.Bounds.Y - Bottom(body) is >= -1 and <= 7
            && CenterX(shape.Bounds) >= body.X && CenterX(shape.Bounds) <= Right(body));

    private static bool IsDot(Int32Rect bounds) => bounds.Width is >= 2 and <= 9 && bounds.Height is >= 2 and <= 9;

    private static Int32Rect Pad(Int32Rect bounds, Int32Rect crop)
    {
        int left = Math.Max(crop.X, bounds.X - 3);
        int top = Math.Max(crop.Y, bounds.Y - 3);
        return new Int32Rect(left, top, Math.Min(Right(crop), Right(bounds) + 3) - left,
            Math.Min(Bottom(crop), Bottom(bounds) + 3) - top);
    }

    private static int CenterX(Int32Rect rect) => rect.X + rect.Width / 2;

    private static int Right(Int32Rect rect) => rect.X + rect.Width;

    private static int Bottom(Int32Rect rect) => rect.Y + rect.Height;

    private sealed record InkShape(Int32Rect Bounds, int Area);
}

internal sealed record SwaraMarks(bool Tivra, Int32Rect? Fallback, Int32Rect? Kan, Int32Rect? Main,
    char? Octave, char? KanOctave, char? MainOctave, string? Symbol);

internal readonly record struct LyricMarks(int Leading, int Trailing, Int32Rect? Body)
{
    internal bool Found => Leading + Trailing > 0;
}
