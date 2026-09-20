using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Sound character: is it pitched, what pitch, and how tonal, noisy, harmonic or bright is it.
///</summary>
///<param name="FundamentalHz">Detected fundamental frequency, or null when no stable pitch was found.</param>
///<param name="MidiNote">Fractional MIDI note number of the fundamental, or null.</param>
///<param name="PitchConfidence">0 - 1, where 1 is a perfectly periodic signal.</param>
///<param name="HarmonicToNoiseDb">Ratio of periodic to aperiodic energy in decibels (higher = more tonal).</param>
///<param name="Inharmonicity">Mean relative deviation of the detected partials from integer multiples of the fundamental.</param>
///<param name="Character">Descriptor flags.</param>
public sealed record TonalityFacet(double? FundamentalHz, double? MidiNote, double PitchConfidence, double HarmonicToNoiseDb, double Inharmonicity, TonalCharacter Character)
{
    #region Public properties

    ///<summary>
    ///Deviation of the fundamental from the nearest equal-tempered note in cents, or null.
    ///</summary>
    [JsonIgnore]
    public double? CentsOffset => MidiNote is null ? null : (MidiNote.Value - Math.Round(MidiNote.Value)) * 100.0;

    ///<summary>
    ///Name of the nearest note (for example <c>F#1</c>), or null when unpitched.
    ///</summary>
    [JsonIgnore]
    public string? NoteName => MidiNote is null ? null : MusicalNotes.NoteName((int)Math.Round(MidiNote.Value));

    ///<summary>
    ///Pitch class (0 = C) of the nearest note, or null when unpitched.
    ///</summary>
    [JsonIgnore]
    public int? PitchClass => MidiNote is null ? null : MusicalNotes.PitchClassOf((int)Math.Round(MidiNote.Value));
    #endregion
}
