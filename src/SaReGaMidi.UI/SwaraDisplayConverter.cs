using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

internal sealed class SwaraDisplayConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string notation || string.IsNullOrWhiteSpace(notation))
            return null;

        IReadOnlyList<SwaraEvent> events;
        try
        {
            events = MaatraNotation.Parse(notation);
        }
        catch (FormatException)
        {
            events = MaatraNotation.Parse("?");
        }

        List<MeendAnchor> anchors = [];
        Grid notes = new();
        notes.Children.Add(MaatraDisplay.CreateSwaraRow(events, anchors));
        if (events.Any(note => note.MeendToNext))
            notes.Children.Add(new MeendLayer(anchors, showOutgoing: true));

        return notes;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
