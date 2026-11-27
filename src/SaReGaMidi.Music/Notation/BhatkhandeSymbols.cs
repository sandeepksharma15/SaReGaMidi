using System;

namespace SaReGaMidi.Music.Notation;

public static class BhatkhandeSymbols
{
    public static BhatkhandeSymbol GetSymbol(SwaraPitch pitch)
    {
        string text = pitch.Swara switch
        {
            Swara.Sa => "सा",
            Swara.Re or Swara.ReKomal => "रे",
            Swara.Ga or Swara.GaKomal => "ग",
            Swara.Ma or Swara.MaTivra => "म",
            Swara.Pa => "प",
            Swara.Dha or Swara.DhaKomal => "ध",
            Swara.Ni or Swara.NiKomal => "नि",
            _ => throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "Unknown swara.")
        };

        ArgumentOutOfRangeException.ThrowIfLessThan((int)pitch.Saptak, (int)Saptak.Mandra, nameof(pitch));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)pitch.Saptak, (int)Saptak.Taar, nameof(pitch));

        return new BhatkhandeSymbol(
            text,
            HasUnderline: pitch.Swara is Swara.ReKomal or Swara.GaKomal or Swara.DhaKomal or Swara.NiKomal,
            HasVerticalStrokeAbove: pitch.Swara == Swara.MaTivra,
            HasDotBelow: pitch.Saptak == Saptak.Mandra,
            HasDotAbove: pitch.Saptak == Saptak.Taar);
    }
}
