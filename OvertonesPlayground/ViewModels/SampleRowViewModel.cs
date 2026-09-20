using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OvertonesPlayground.Ontology.Concepts;
using OvertonesPlayground.Ontology.Facets;
using OvertonesPlayground.Ontology.Model;

namespace OvertonesPlayground.ViewModels;

///<summary>
///One line in the Sound Bank result list: the name, what the ontology says the sound is, and a few badges for the facts that
///matter when choosing a sound (length, stereo, loudness, pitch, tempo).
///</summary>
public partial class SampleRowViewModel : ObservableObject
{
    #region Constructors
    ///<summary>
    ///Creates the row for <paramref name="sample"/>.
    ///</summary>
    public SampleRowViewModel(Sample sample)
    {
        Sample = sample;
        Subtitle = BuildSubtitle(sample);
        Badges = BuildBadges(sample);
    }
    #endregion

    #region Private methods
    private static string BuildSubtitle(Sample sample)
    {
        List<string> parts = [];
        bool isKnown = Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? instrument);
        parts.Add(isKnown && sample.Classification.Instrument.Value != "unclassified" ? instrument!.Path : "Unclassified");

        if (sample.Classification.Kit is not null && Taxonomies.Kits.TryGet(sample.Classification.Kit.Value, out KitConcept? kit))
        {
            parts.Add(kit.DisplayName);
        }

        return string.Join("  ·  ", parts);
    }

    private static IReadOnlyList<string> BuildBadges(Sample sample)
    {
        List<string> badges = [];
        if (sample.Technical is { } technical)
        {
            badges.Add(FormatDuration(technical.DurationSeconds));
        }

        if (sample.Stereo is { } stereo)
        {
            badges.Add(stereo.Image switch
            {
                StereoImage.DualMono => "Mono",
                StereoImage.OutOfPhase => "Out of phase",
                _ => stereo.Image.ToString(),
            });
        }

        if (sample.Dynamics is { } dynamics)
        {
            badges.Add(dynamics.Loudness.ToString());
        }

        if (sample.Tonality is { NoteName: { } note, Character: var character } && character.HasFlag(TonalCharacter.Pitched))
        {
            badges.Add(note);
        }

        if (sample.EffectiveTempoBpm is { } tempo)
        {
            badges.Add($"{tempo.ToString("0", CultureInfo.InvariantCulture)} BPM");
        }

        if (sample.Classification.ContentType.Value is not (ContentType.OneShot or ContentType.Unknown))
        {
            badges.Add(sample.Classification.ContentType.Value.ToString());
        }

        return badges;
    }

    ///<summary>
    ///Formats a length for display: milliseconds under a second, otherwise seconds.
    ///</summary>
    internal static string FormatDuration(double seconds) =>
        seconds < 1.0
            ? $"{(seconds * 1000).ToString("0", CultureInfo.InvariantCulture)} ms"
            : seconds < 60.0 ? $"{seconds.ToString("0.0", CultureInfo.InvariantCulture)} s" : $"{(int)(seconds / 60)}:{(int)(seconds % 60):00}";
    #endregion

    #region Public properties
    ///<summary>
    ///Short facts about the sound, shown as small tags.
    ///</summary>
    public IReadOnlyList<string> Badges { get; }

    ///<summary>
    ///Whether the sound is currently being auditioned.
    ///</summary>
    [ObservableProperty]
    public partial bool IsPreviewing { get; set; }

    ///<summary>
    ///Whether this is the row shown in the detail panel.
    ///</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    ///<summary>
    ///The sound's display name.
    ///</summary>
    public string Name => Sample.Name;

    ///<summary>
    ///Whether a maintainer should look at how this sound was classified.
    ///</summary>
    public bool NeedsReview => Sample.Classification.NeedsReview;

    ///<summary>
    ///The sound this row describes.
    ///</summary>
    public Sample Sample { get; }

    ///<summary>
    ///Instrument path and kit, for example "Percussion > Hi-Hat > Closed Hi-Hat  ·  Roland 909".
    ///</summary>
    public string Subtitle { get; }
    #endregion
}
