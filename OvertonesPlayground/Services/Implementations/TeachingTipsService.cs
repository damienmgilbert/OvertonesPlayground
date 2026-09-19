using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="ITeachingTipsService"/>
///<remarks>
///Stored in <see cref="IPreferences"/>: it is a handful of flags, not user content. Preferences can't be enumerated, so the keys
///are also kept in one list for <see cref="ResetAll"/> to find.
///</remarks>
public class TeachingTipsService : ITeachingTipsService
{
    #region Constants
    private const string IndexKey = "TeachingTips.Keys";
    private const char Separator = ',';
    private const string SeenPrefix = "TeachingTips.Seen.";
    #endregion

    #region Fields
    private readonly IPreferences _preferences;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the teaching-tips service.
    ///</summary>
    ///<param name="preferences">Where the seen flags are kept between runs.</param>
    public TeachingTipsService(IPreferences preferences)
    {
        _preferences = preferences;
    }
    #endregion

    #region Private methods
    private string[] ReadIndex() => _preferences.Get(IndexKey, string.Empty).Split(Separator, StringSplitOptions.RemoveEmptyEntries);
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public bool HasSeen(string key) => _preferences.Get(SeenPrefix + key, false);

    ///<inheritdoc/>
    public void MarkSeen(string key)
    {
        _preferences.Set(SeenPrefix + key, true);

        HashSet<string> keys = [.. ReadIndex(), key];
        _preferences.Set(IndexKey, string.Join(Separator, keys));
    }

    ///<inheritdoc/>
    public void ResetAll()
    {
        foreach (string key in ReadIndex())
        {
            _preferences.Remove(SeenPrefix + key);
        }

        _preferences.Remove(IndexKey);
    }
    #endregion
}
