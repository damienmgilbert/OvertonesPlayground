namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Remembers which one-time tips the user has already seen, so each shows once and can be brought back from Settings.
///</summary>
public interface ITeachingTipsService
{
    #region Methods
    ///<summary>
    ///Whether the tip with this key has been shown and dismissed before.
    ///</summary>
    bool HasSeen(string key);

    ///<summary>
    ///Records that the tip with this key has been shown and dismissed.
    ///</summary>
    void MarkSeen(string key);

    ///<summary>
    ///Forgets every tip that has been seen, so each shows again the next time its page opens.
    ///</summary>
    void ResetAll();
    #endregion
}
