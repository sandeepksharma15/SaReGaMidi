using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SaReGaMidi.Music;
using SaReGaMidi.Music.Score;

namespace SaReGaMidi.UI;

public partial class TranscriptionWindow
{
    private void AnalysisSa_Changed(object sender, SelectionChangedEventArgs e) => RefreshAnalysis();

    private void RareCutoff_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => RefreshAnalysis();

    private void RefreshAnalysis()
    {
        if (ScoreTabs?.SelectedIndex != 2 || AnalysisResults is null || AnalysisSa.SelectedIndex < 0)
            return;

        AnalysisResults.Children.Clear();
        IReadOnlyList<SwaraUsage> usage;
        try
        {
            usage = ScaleAnalysis.CountNotes(_rows.Where(row => !string.IsNullOrWhiteSpace(row.Swaras))
                .SelectMany(row => MaatraNotation.Parse(row.Swaras)));
        }
        catch (FormatException exception)
        {
            AnalysisSummary.Text = $"Check the written swaras before analysing the score. {exception.Message}";
            return;
        }

        int total = usage.Sum(note => note.Count);
        if (total == 0)
        {
            AnalysisSummary.Text = "Add melody notes to see the scale.";
            return;
        }

        int tonic = AnalysisSa.SelectedIndex + 48;
        int cutoff = (int)RareCutoff.Value;
        AnalysisSummary.Text = $"{total} written melody-note attacks across all octaves. Kan swaras are counted separately.";
        SwaraUsage[] frequent = usage.Where(note => note.IsFrequent(total, cutoff)).ToArray();
        AddAnalysisGroup("Frequent swaras", frequent, tonic);
        string? scale = ScaleAnalysis.MatchWesternScale(frequent.Select(note => note.Swara));
        TextBlock match = new() { TextWrapping = TextWrapping.Wrap, Text = scale is null
            ? "No exact Western scale match in the catalogue."
            : $"Western scale · {WesternNoteNames.Format(tonic, includeOctave: false)} {scale}" };
        AnalysisResults.Children.Add(match);
        AddAnalysisGroup("Rare swaras", usage.Where(note => note.Count > 0 && !note.IsFrequent(total, cutoff)), tonic);
        SwaraUsage[] ornaments = usage.Where(note => note.Count == 0 && note.KanCount > 0).ToArray();
        if (ornaments.Length > 0)
            AddAnalysisGroup("Kan-only swaras", ornaments, tonic);
    }

    private void AddAnalysisGroup(string title, IEnumerable<SwaraUsage> notes, int tonic)
    {
        AnalysisResults.Children.Add(new TextBlock
        {
            Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 6)
        });
        WrapPanel row = new();
        AnalysisResults.Children.Add(row);
        foreach (SwaraUsage note in notes)
        {
            StackPanel pair = new() { MinWidth = 42, HorizontalAlignment = HorizontalAlignment.Center };
            pair.Children.Add(SwaraGlyph.Create(new SwaraPitch(note.Swara), 20));
            pair.Children.Add(new TextBlock { Text = WesternNoteNames.Format(tonic, note.Swara), HorizontalAlignment = HorizontalAlignment.Center });
            Border card = new()
            {
                Child = pair, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1),
                Padding = new Thickness(3, 4, 3, 6), Margin = new Thickness(0, 0, 6, 6),
                ToolTip = $"{note.Swara} · melody attacks {note.Count} · kan occurrences {note.KanCount}"
            };
            card.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
            row.Children.Add(card);
        }
        if (row.Children.Count == 0)
            row.Children.Add(new TextBlock { Text = "None at this cutoff." });
    }
}
