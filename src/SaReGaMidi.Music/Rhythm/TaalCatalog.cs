using System;
using System.Collections.Generic;
using System.Linq;

namespace SaReGaMidi.Music.Rhythm;

public static class TaalCatalog
{
    public static IReadOnlyList<Taal> All { get; } = Array.AsReadOnly(new Taal[]
    {
        new("Teental",
            new Vibhaag("X", "धा", "धिं", "धिं", "धा"),
            new Vibhaag("2", "धा", "धिं", "धिं", "धा"),
            new Vibhaag("0", "धा", "तिं", "तिं", "ता"),
            new Vibhaag("3", "ता", "धिं", "धिं", "धा")),

        new("Jhaptal",
            new Vibhaag("X", "धी", "ना"),
            new Vibhaag("2", "धी", "धी", "ना"),
            new Vibhaag("0", "ती", "ना"),
            new Vibhaag("3", "धी", "धी", "ना")),

        new("Keharwa",
            new Vibhaag("X", "धा", "गे", "ना", "तिं"),
            new Vibhaag("0", "ना", "क", "धी", "ना")),

        new("Dadra",
            new Vibhaag("X", "धा", "धी", "ना"),
            new Vibhaag("0", "धा", "ती", "ना")),

        new("Rupak",
            new Vibhaag("0", "तिं", "तिं", "ना"),
            new Vibhaag("1", "धी", "ना"),
            new Vibhaag("2", "धी", "ना")),

        new("Sooltal",
            new Vibhaag("X", "धा", "धा"),
            new Vibhaag("0", "दिन", "ता"),
            new Vibhaag("2", "किट", "धा"),
            new Vibhaag("3", "तिट", "कत"),
            new Vibhaag("0", "गदि", "गन")),

        new("Ektal",
            new Vibhaag("X", "धिं", "धिं"),
            new Vibhaag("0", "धागे", "तिरकिट"),
            new Vibhaag("2", "तू", "ना"),
            new Vibhaag("0", "कत", "ता"),
            new Vibhaag("3", "धागे", "तिरकिट"),
            new Vibhaag("4", "धिं", "ना")),

        new("Chautal",
            new Vibhaag("X", "धा", "धा"),
            new Vibhaag("0", "दिन", "ता"),
            new Vibhaag("2", "किट", "धा"),
            new Vibhaag("0", "दिन", "ता"),
            new Vibhaag("3", "तिट", "कत"),
            new Vibhaag("4", "गदि", "गन")),

        new("Tilwada",
            new Vibhaag("X", "धा", "तिरकिट", "धिं", "धिं"),
            new Vibhaag("2", "धा", "धा", "तिं", "तिं"),
            new Vibhaag("0", "ता", "तिरकिट", "धिं", "धिं"),
            new Vibhaag("3", "धा", "धा", "धिं", "धिं")),

        new("Deepchandi",
            new Vibhaag("X", "धा", "धिं", null),
            new Vibhaag("2", "धा", "धा", "तिं", null),
            new Vibhaag("0", "ता", "तिं", null),
            new Vibhaag("3", "धा", "धा", "धिं", null))
    });

    public static Taal? FindByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string trimmedName = name.Trim();
        return All.FirstOrDefault(taal => string.Equals(taal.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
    }
}
