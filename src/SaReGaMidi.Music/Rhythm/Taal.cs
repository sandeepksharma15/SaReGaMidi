using System;
using System.Collections.Generic;
using System.Linq;

namespace SaReGaMidi.Music.Rhythm;

public sealed class Taal
{
    internal Taal(string name, params Vibhaag[] vibhaags)
    {
        Name = name;
        Vibhaags = Array.AsReadOnly(vibhaags);
        MaatraCount = vibhaags.Sum(vibhaag => vibhaag.Theka.Count);
    }

    public string Name { get; }

    public int MaatraCount { get; }

    /// <summary>The ordered divisions of the cycle. Sam falls at the start of the first division.</summary>
    public IReadOnlyList<Vibhaag> Vibhaags { get; }
}
