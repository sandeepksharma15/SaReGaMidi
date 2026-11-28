using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

internal sealed class MaatraDisplay : StackPanel
{
    private const double CompactWidth = 45;
    private const double MinimumSwaraSize = 11;

    internal MaatraDisplay(IReadOnlyList<SwaraEvent> events, string lyrics, bool grouped, bool preview = false,
        bool blank = false)
    {
        Grid notes = new() { HorizontalAlignment = HorizontalAlignment.Center };
        StackPanel glyphs = CreateSwaraRow(events, Anchors);
        notes.Children.Add(glyphs);

        if (events.Count == 0 && !blank)
            glyphs.Children.Add(new TextBlock { Text = "+", VerticalAlignment = VerticalAlignment.Center });

        if (preview)
        {
            glyphs.Margin = new Thickness(12, 8, 12, 0);
            notes.Children.Add(new MeendLayer(Anchors, showOutgoing: true));
        }

        Children.Add(notes);
        if (grouped && events.Count > 1)
        {
            Path curve = new()
            {
                Data = Geometry.Parse("M 0,0 Q 20,6 40,0"), Stretch = Stretch.Fill,
                Height = 5, StrokeThickness = 1, Margin = new Thickness(2, 0, 2, 1)
            };
            curve.SetResourceReference(Shape.StrokeProperty, "InkBrush");
            Children.Add(curve);
        }
        else
        {
            Children.Add(new Border { Height = 6 });
        }

        Children.Add(new TextBlock
        {
            Text = lyrics, FontFamily = new FontFamily("Nirmala UI"), FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center, MinHeight = 23
        });
    }

    internal List<MeendAnchor> Anchors { get; } = [];

    internal static StackPanel CreateSwaraRow(IReadOnlyList<SwaraEvent> events, List<MeendAnchor> anchors)
    {
        StackPanel glyphs = new() { Orientation = Orientation.Horizontal, Height = 32 };
        double size = events.Count >= 4 ? 14 : 18;
        double spacing = events.Count > 1 ? 0.5 : 2;
        AddGlyphs(glyphs, events, anchors, size, spacing);

        if (events.Count > 1)
        {
            glyphs.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double width = glyphs.DesiredSize.Width;
            if (width > CompactWidth)
            {
                // Reserve the fixed side spacing when scaling the glyphs to the beat column.
                double padding = spacing * 2 * events.Count;
                double fittedSize = size * (CompactWidth - padding) / (width - padding);
                AddGlyphs(glyphs, events, anchors, Math.Max(MinimumSwaraSize, fittedSize), spacing);
            }
        }

        return glyphs;
    }

    private static void AddGlyphs(StackPanel panel, IReadOnlyList<SwaraEvent> events, List<MeendAnchor> anchors,
        double size, double spacing)
    {
        panel.Children.Clear();
        anchors.Clear();

        foreach (SwaraEvent note in events)
        {
            FrameworkElement glyph = SwaraGlyph.Create(note, size, spacing);
            panel.Children.Add(glyph);
            anchors.Add(new MeendAnchor(note, glyph));
        }
    }
}

internal sealed record MeendAnchor(SwaraEvent Note, FrameworkElement Glyph);

internal sealed record MeendSegment(MeendAnchor? Start, MeendAnchor? End);

internal sealed class MeendLayer : FrameworkElement
{
    private readonly DrawingVisual _drawing = new();
    private readonly IReadOnlyList<MeendAnchor> _anchors;
    private readonly bool _showOutgoing;
    private readonly bool _showIncoming;
    private readonly IReadOnlyList<MeendSegment> _segments;

    internal MeendLayer(IReadOnlyList<MeendAnchor> anchors, bool showOutgoing, bool showIncoming = false,
        IReadOnlyList<MeendSegment>? segments = null)
    {
        _anchors = anchors;
        _showOutgoing = showOutgoing;
        _showIncoming = showIncoming;
        _segments = segments ?? [];
        IsHitTestVisible = false;
        AddVisualChild(_drawing);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _drawing;

    protected override Size ArrangeOverride(Size finalSize)
    {
        using DrawingContext drawing = _drawing.RenderOpen();
        Pen pen = new(TryFindResource("InkBrush") as Brush ?? Brushes.Black, 1.35);
        Point? start = null;
        bool incoming = _showIncoming;

        foreach (MeendAnchor anchor in _anchors)
        {
            if (anchor.Note.Kind == SwaraEventKind.Sustain)
                continue;

            if (anchor.Note.Kind == SwaraEventKind.Note)
            {
                Point end = GetPoint(anchor);
                if (start is Point origin)
                    DrawCurve(drawing, pen, origin, end);
                else if (incoming)
                    DrawCurve(drawing, pen, new Point(0, end.Y - 4), end);

                start = anchor.Note.MeendToNext ? end : null;
            }
            else
            {
                start = null;
            }

            incoming = false;
        }

        if (_showOutgoing && start is Point last)
            DrawCurve(drawing, pen, last, new Point(finalSize.Width, last.Y - 4));

        foreach (MeendSegment segment in _segments)
        {
            Point? first = segment.Start is MeendAnchor from ? GetPoint(from) : null;
            Point? lastPoint = segment.End is MeendAnchor to ? GetPoint(to) : null;
            DrawCurve(drawing, pen,
                first ?? new Point(0, (lastPoint?.Y ?? 20) - 4),
                lastPoint ?? new Point(finalSize.Width, (first?.Y ?? 20) - 4));
        }

        return finalSize;
    }

    private Point GetPoint(MeendAnchor anchor)
        => anchor.Glyph.TranslatePoint(new Point(anchor.Glyph.ActualWidth / 2, 0), this);

    private static void DrawCurve(DrawingContext drawing, Pen pen, Point start, Point end)
    {
        double height = Math.Max(1, Math.Min(start.Y, end.Y) - 8);
        StreamGeometry geometry = new();
        using (StreamGeometryContext curve = geometry.Open())
        {
            curve.BeginFigure(start, isFilled: false, isClosed: false);
            curve.QuadraticBezierTo(new Point((start.X + end.X) / 2, height), end, isStroked: true, isSmoothJoin: false);
        }

        drawing.DrawGeometry(null, pen, geometry);
    }
}
