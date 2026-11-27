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
    internal MaatraDisplay(IReadOnlyList<SwaraEvent> events, string lyrics, bool grouped, bool preview = false)
    {
        Grid notes = new() { HorizontalAlignment = HorizontalAlignment.Center };
        StackPanel glyphs = new() { Orientation = Orientation.Horizontal, Height = 32 };
        notes.Children.Add(glyphs);

        foreach (SwaraEvent note in events)
        {
            FrameworkElement glyph = SwaraGlyph.Create(note);
            glyphs.Children.Add(glyph);
            Anchors.Add(new MeendAnchor(note, glyph));
        }

        if (events.Count == 0)
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
}

internal sealed record MeendAnchor(SwaraEvent Note, FrameworkElement Glyph);

internal sealed class MeendLayer : FrameworkElement
{
    private readonly DrawingVisual _drawing = new();
    private readonly IReadOnlyList<MeendAnchor> _anchors;
    private readonly bool _showOutgoing;
    private readonly bool _showIncoming;

    internal MeendLayer(IReadOnlyList<MeendAnchor> anchors, bool showOutgoing, bool showIncoming = false)
    {
        _anchors = anchors;
        _showOutgoing = showOutgoing;
        _showIncoming = showIncoming;
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
                Point end = anchor.Glyph.TranslatePoint(new Point(anchor.Glyph.ActualWidth / 2, 0), this);
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

        return finalSize;
    }

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
