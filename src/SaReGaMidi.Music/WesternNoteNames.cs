using System;

namespace SaReGaMidi.Music;

public static class WesternNoteNames
{
    private const string Letters = "CDEFGAB";
    private static readonly int[] _naturalOffsets = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] _tonicLetters = [0, 0, 1, 2, 2, 3, 3, 4, 5, 5, 6, 6];
    private static readonly int[] _swaraDegrees = [0, 1, 1, 2, 2, 3, 3, 4, 5, 5, 6, 6];

    public static string Format(int tonic, Swara swara = Swara.Sa, bool includeOctave = true)
    {
        int midi = PitchConverter.GetMidiNote(new SwaraPitch(swara), tonic);
        int degree = _tonicLetters[tonic % 12] + _swaraDegrees[(int)swara];
        int letter = degree % 7;
        int octave = tonic / 12 - 1 + degree / 7;
        int accidental = midi - ((octave + 1) * 12 + _naturalOffsets[letter]);
        string suffix = accidental < 0 ? new string('♭', -accidental) : new string('♯', accidental);
        return $"{Letters[letter]}{suffix}{(includeOctave ? octave.ToString() : string.Empty)}";
    }
}
