namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///One way a concept can be named in a file name: a run of normalized words.
///</summary>
///<typeparam name="TConcept">Concept kind.</typeparam>
///<param name="Concept">The concept the words name.</param>
///<param name="Tokens">The words, lower-case.</param>
///<param name="StartOnly">The words only count at the very start of the name (written <c>^808</c> in the data).</param>
public sealed record ConceptAlias<TConcept>(TConcept Concept, string[] Tokens, bool StartOnly)
    where TConcept : OntologyConcept;
