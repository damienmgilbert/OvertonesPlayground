namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///Keyword sets for the classification axes that are enums rather than concept trees (content type, sound origin),
///plus filler words that are never evidence.
///</summary>
public sealed class Lexicon
{
    #region Constructors
    internal Lexicon(LexiconFile file)
    {
        ContentTypeKeywords = ToMap<ContentType>(file.ContentTypes);
        OriginKeywords = ToMap<SoundOrigin>(file.Origins);
        IgnoredTokens = new HashSet<string>(file.IgnoredTokens.SelectMany(TextNormalizer.Tokenize), StringComparer.Ordinal);
    }
    #endregion

    #region Private methods
    private static Dictionary<T, HashSet<string>> ToMap<T>(Dictionary<string, List<string>> source)
        where T : struct, Enum
    {
        Dictionary<T, HashSet<string>> map = [];
        foreach ((string name, List<string> words) in source)
        {
            map[Enum.Parse<T>(name, ignoreCase: true)] = new HashSet<string>(words.SelectMany(TextNormalizer.Tokenize), StringComparer.Ordinal);
        }

        return map;
    }
    #endregion

    #region Public properties
    ///<summary>Words that signal a content type, for example <c>riser</c> for <see cref="ContentType.Transition"/>.</summary>
    public IReadOnlyDictionary<ContentType, HashSet<string>> ContentTypeKeywords { get; }

    ///<summary>Words that are never evidence (<c>and</c>, <c>the</c> ...).</summary>
    public HashSet<string> IgnoredTokens { get; }

    ///<summary>Words that signal how a sound was produced.</summary>
    public IReadOnlyDictionary<SoundOrigin, HashSet<string>> OriginKeywords { get; }
    #endregion
}
