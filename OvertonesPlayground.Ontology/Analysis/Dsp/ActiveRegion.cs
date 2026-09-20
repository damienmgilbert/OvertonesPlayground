namespace OvertonesPlayground.Ontology.Analysis.Dsp;

///<summary>
///Region of a signal that is audibly present, in sample frames.
///</summary>
///<param name="StartFrame">First active frame.</param>
///<param name="EndFrame">One past the last active frame.</param>
public readonly record struct ActiveRegion(int StartFrame, int EndFrame)
{

    ///<summary>
    ///Length in frames.
    ///</summary>
    public int Length => Math.Max(0, EndFrame - StartFrame);
}
