using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SaReGaMidi.Music;
using SaReGaMidi.Music.Notation;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

internal static class SwaraGlyph
{
    internal static FrameworkElement Create(SwaraEvent note, double size = 18, double spacing = 2)
    {
        if (note.Pitch is SwaraPitch pitch)
        {
            FrameworkElement glyph = Create(pitch, size, spacing);
            if (note.Khatka)
            {
                StackPanel bracketed = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                bracketed.Children.Add(CreateLabel("(", size));
                bracketed.Children.Add(glyph);
                bracketed.Children.Add(CreateLabel(")", size));
                glyph = bracketed;
            }

            if (note.KanSwara is not SwaraPitch kan)
                return glyph;

            StackPanel pair = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            FrameworkElement grace = Create(kan, size * 11 / 18, spacing);
            grace.VerticalAlignment = VerticalAlignment.Top;
            grace.Margin = new Thickness(0, -3, 0, 0);
            pair.Children.Add(grace);
            pair.Children.Add(glyph);
            return pair;
        }

        return CreateLabel(note.Kind switch
        {
            SwaraEventKind.Sustain => "–",
            SwaraEventKind.Silence => "*",
            _ => "?"
        }, size);
    }

    internal static FrameworkElement Create(SwaraPitch pitch, double size = 18, double spacing = 2)
    {
        BhatkhandeSymbol symbol = BhatkhandeSymbols.GetSymbol(pitch);
        Grid glyph = new() { Margin = new Thickness(spacing, 0, spacing, 0), VerticalAlignment = VerticalAlignment.Center };
        glyph.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
        glyph.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        glyph.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });

        if (symbol.HasVerticalStrokeAbove)
        {
            FormattedText text = new(symbol.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Nirmala UI"), size, Brushes.Black, pixelsPerDip: 1);
            double headlineTop = text.BuildGeometry(new Point()).Bounds.Top;
            AddMark(glyph, new Rectangle { Width = 1, Height = 5 }, 0, new Thickness(0, 0, 0, -(headlineTop + 0.5)));
        }

        if (symbol.HasDotAbove)
            AddMark(glyph, new Ellipse { Width = 3, Height = 3 }, 0, new Thickness(8, 0, 0, -3));

        TextBlock label = CreateLabel(symbol.Text, size);
        if (symbol.HasUnderline)
            label.TextDecorations = TextDecorations.Underline;

        Grid.SetRow(label, 1);
        glyph.Children.Add(label);

        if (symbol.HasDotBelow)
            AddMark(glyph, new Ellipse { Width = 3, Height = 3 }, 2, new Thickness(0));

        return glyph;
    }

    private static TextBlock CreateLabel(string text, double size = 18)
        => new()
        {
            Text = text,
            FontFamily = new FontFamily("Nirmala UI"),
            FontSize = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

    private static void AddMark(Grid glyph, Shape mark, int row, Thickness margin)
    {
        mark.Margin = margin;
        mark.HorizontalAlignment = HorizontalAlignment.Center;
        mark.VerticalAlignment = row == 0 ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        mark.SetResourceReference(Shape.FillProperty, "InkBrush");
        Grid.SetRow(mark, row);
        glyph.Children.Add(mark);
    }
}
