using System;
using System.Collections.Generic;

namespace SaReGaMidi.Music.Rhythm;

public sealed class Vibhaag
{
    internal Vibhaag(string mark, params string?[] theka)
    {
        Mark = mark;
        Theka = Array.AsReadOnly(theka);
    }

    public string Mark { get; }

    public bool IsKhali => Mark == "0";

    /// <summary>One entry per maatra. A null entry is a rest.</summary>
    public IReadOnlyList<string?> Theka { get; }
}
