namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///Turns file names and aliases into comparable word lists: lower case, separators (spaces, hyphens, underscores,
///dots ...) split words, apostrophes vanish, and <c>#</c> stays (so <c>F#</c> is one word).
///</summary>
public static class TextNormalizer
{
    #region Public methods
    ///<summary>Splits <paramref name="text"/> into normalized words.</summary>
    public static string[] Tokenize(string text)
    {
        List<string> tokens = [];
        System.Text.StringBuilder current = new();
        foreach (char c in text)
        {
            bool isWordChar = char.IsLetterOrDigit(c) || c == '#';
            bool isApostrophe = c is '\'' or '’';
            if (isWordChar)
            {
                _ = current.Append(char.ToLowerInvariant(c));
            }
            else if (!isApostrophe && current.Length > 0)
            {
                tokens.Add(current.ToString());
                _ = current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return [.. tokens];
    }
    #endregion
}
