using System;

namespace SaReGaMidi.Music;

public static class PitchConverter
{
    /// <summary>Resolves a swara to an equal-tempered MIDI note number using madhya Sa as the tonic.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The pitch is invalid or the tonic or result is outside MIDI's 0–127 range.</exception>
    public static int GetMidiNote(SwaraPitch pitch, int saMidiNote)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(saMidiNote);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(saMidiNote, 127);
        ArgumentOutOfRangeException.ThrowIfNegative((int)pitch.Swara, nameof(pitch));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)pitch.Swara, (int)Swara.Ni, nameof(pitch));
        ArgumentOutOfRangeException.ThrowIfLessThan((int)pitch.Saptak, (int)Saptak.Mandra, nameof(pitch));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)pitch.Saptak, (int)Saptak.Taar, nameof(pitch));

        int midiNote = saMidiNote + (int)pitch.Swara + (12 * (int)pitch.Saptak);

        if (midiNote is < 0 or > 127)
            throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "The pitch is outside the MIDI note range for this Sa.");

        return midiNote;
    }
}
