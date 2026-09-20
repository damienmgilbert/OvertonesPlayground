namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Result of analysing a file.
///</summary>
public enum AnalysisStatus
{
    ///<summary>Every facet was extracted.</summary>
    Analyzed,

    ///<summary>The audio was readable but at least one analyzer failed or was skipped.</summary>
    Partial,

    ///<summary>The container is not supported (for example AIFF-C); only the filename labels exist.</summary>
    UnsupportedFormat,

    ///<summary>The file could not be read.</summary>
    Failed,
}

///<summary>
///Structural role of a sample in a production, independent of what instrument it is.
///</summary>
public enum ContentType
{
    ///<summary>Not determined.</summary>
    Unknown,

    ///<summary>A single hit or note.</summary>
    OneShot,

    ///<summary>A repeating musical phrase that loops seamlessly.</summary>
    Loop,

    ///<summary>A drum break or chopped groove.</summary>
    Break,

    ///<summary>A non-repeating musical phrase or vocal line.</summary>
    Phrase,

    ///<summary>A short chord or orchestra hit.</summary>
    Stab,

    ///<summary>A sustained bed: atmosphere, ambience, noise, pad.</summary>
    Texture,

    ///<summary>A riser, sweep, swell, impact or other transition effect.</summary>
    Transition,
}

///<summary>
///How a sound was originally produced.
///</summary>
public enum SoundOrigin
{
    ///<summary>Not determined.</summary>
    Unknown,

    ///<summary>A real acoustic instrument or voice.</summary>
    Acoustic,

    ///<summary>An analog synthesizer or analog drum machine.</summary>
    Analog,

    ///<summary>A digital synthesizer or digital sound source.</summary>
    Digital,

    ///<summary>A classic drum machine or sampler workstation.</summary>
    DrumMachine,

    ///<summary>Sampled from vinyl or another recorded source.</summary>
    Sampled,

    ///<summary>Heavily processed or effected.</summary>
    Processed,
}

///<summary>
///Major or minor mode written in a file name.
///</summary>
public enum KeyMode
{
    ///<summary>No mode given (a single note or an unspecified key).</summary>
    None,

    ///<summary>Major.</summary>
    Major,

    ///<summary>Minor.</summary>
    Minor,
}

///<summary>
///Where a piece of evidence for a label came from.
///</summary>
public enum EvidenceSource
{
    ///<summary>Words in the file name.</summary>
    Filename,

    ///<summary>The measured audio (facets, nearest-neighbour vote).</summary>
    Signal,

    ///<summary>Metadata embedded in the file (smpl / acid / iXML chunks).</summary>
    Metadata,

    ///<summary>A hand-written override.</summary>
    Manual,
}

///<summary>
///How two samples relate to each other.
///</summary>
public enum SampleRelationType
{
    ///<summary>Belong to the same kit (for example both are Roland 909).</summary>
    SameKit,

    ///<summary>Numbered variations of the same sound.</summary>
    VariationOf,

    ///<summary>Complementary drum-kit pieces (kick and snare of the same kit).</summary>
    Complements,

    ///<summary>The pitched fundamentals share a pitch class or key.</summary>
    PitchCompatible,

    ///<summary>The tempos match (allowing half / double time).</summary>
    TempoCompatible,

    ///<summary>Close neighbours in acoustic feature space.</summary>
    SimilarTo,
}
