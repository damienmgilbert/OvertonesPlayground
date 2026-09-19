namespace OvertonesPlayground.Models;

///<summary>
///Everything the Launchpad remembers: the pads in all four banks, the column mixer, the sequencer and the global settings. It
///is what is saved as the current layout, and what Projects saves and opens by name.
///</summary>
public class LaunchpadProject
{
    #region Constants
    ///<summary>
    ///The number of pad banks.
    ///</summary>
    public const int BankCount = 4;

    ///<summary>
    ///The number of pads in a bank.
    ///</summary>
    public const int PadsPerBank = 64;

    ///<summary>
    ///The number of pad columns.
    ///</summary>
    public const int ColumnCount = 8;
    #endregion

    #region Public methods
    ///<summary>
    ///Makes sure every part is complete, after loading a file that may be missing some.
    ///</summary>
    public void Normalize()
    {
        while (Columns.Count < ColumnCount)
        {
            Columns.Add(new LaunchpadColumn());
        }

        Sequence.Normalize();
        Bank = Math.Clamp(Bank, 0, BankCount - 1);
        Tempo = Math.Clamp(Tempo, 40, 240);
        SwingLevel = Math.Clamp(SwingLevel, 0, 7);
        MasterVolume = Math.Clamp(MasterVolume, 0, 1);
        MasterPan = Math.Clamp(MasterPan, -1, 1);
        Transpose = Math.Clamp(Transpose, -12, 12);
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The bank whose pads are on screen (0 to 3).
    ///</summary>
    public int Bank { get; set; }

    ///<summary>
    ///The eight columns' mixer settings.
    ///</summary>
    public List<LaunchpadColumn> Columns { get; set; } = [.. Enumerable.Range(0, ColumnCount).Select(_ => new LaunchpadColumn())];

    ///<summary>
    ///Master pan, from -1 to 1.
    ///</summary>
    public double MasterPan { get; set; }

    ///<summary>
    ///Master volume, from 0 to 1.
    ///</summary>
    public double MasterVolume { get; set; } = 1;

    ///<summary>
    ///The project's name; also its file name when saved.
    ///</summary>
    public string Name { get; set; } = string.Empty;

    ///<summary>
    ///Every pad that has a sample, in all banks.
    ///</summary>
    public List<LaunchpadPad> Pads { get; set; } = [];

    ///<summary>
    ///Which scale Note and Chord modes use (an index into <see cref="LaunchpadScale"/>).
    ///</summary>
    public int ScaleIndex { get; set; }

    ///<summary>
    ///The sequencer.
    ///</summary>
    public LaunchpadSequence Sequence { get; set; } = new();

    ///<summary>
    ///Swing amount, as one of eight levels (0 is none).
    ///</summary>
    public int SwingLevel { get; set; }

    ///<summary>
    ///Tempo in beats per minute.
    ///</summary>
    public int Tempo { get; set; } = 120;

    ///<summary>
    ///How many semitones everything is transposed (-12 to 12).
    ///</summary>
    public int Transpose { get; set; }
    #endregion
}
