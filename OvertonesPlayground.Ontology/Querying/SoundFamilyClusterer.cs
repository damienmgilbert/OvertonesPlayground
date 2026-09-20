namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///Groups samples into <see cref="SoundFamily"/>s with k-means on their standardized fingerprints. It is deterministic: the
///seeding uses a fixed seed and ties are broken by name, so the same catalog always gives the same families.
///</summary>
internal static class SoundFamilyClusterer
{
    #region Constants
    private const int MaxIterations = 60;
    private const int Seed = 1729;
    #endregion

    #region Private methods
    ///<summary>About sqrt(n / 2) families, the usual rule of thumb: 33 for the 2,220 bundled sounds.</summary>
    private static int DefaultCount(int points) => Math.Max(1, (int)Math.Round(Math.Sqrt(points / 2.0)));

    private static string FamilyKey(Sample sample) =>
        Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept)
            ? concept.Family.Key
            : sample.Classification.Instrument.Value;

    private static double SquaredDistance(double[] a, double[] b)
    {
        double sum = 0;
        for (int d = 0; d < a.Length; d++)
        {
            double delta = a[d] - b[d];
            sum += delta * delta;
        }

        return sum;
    }

    ///<summary>k-means++ seeding: each next center is drawn with probability proportional to its squared distance to the nearest center chosen so far.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Nothing here is security-sensitive; a fixed seed is what makes the clustering reproducible.")]
    private static double[][] SeedCenters(List<(Sample Sample, double[] Vector)> points, int k)
    {
        Random random = new(Seed);
        double[][] centers = new double[k][];
        centers[0] = [.. points[random.Next(points.Count)].Vector];
        double[] nearest = [.. points.Select(point => SquaredDistance(point.Vector, centers[0]))];
        for (int c = 1; c < k; c++)
        {
            double total = nearest.Sum();
            int chosen = points.Count - 1;
            if (total > 0)
            {
                double target = random.NextDouble() * total;
                double running = 0;
                for (int i = 0; i < nearest.Length; i++)
                {
                    running += nearest[i];
                    if (running >= target)
                    {
                        chosen = i;
                        break;
                    }
                }
            }

            centers[c] = [.. points[chosen].Vector];
            for (int i = 0; i < nearest.Length; i++)
            {
                nearest[i] = Math.Min(nearest[i], SquaredDistance(points[i].Vector, centers[c]));
            }
        }

        return centers;
    }

    ///<summary>Moves every sample to its nearest center. Returns whether any sample changed cluster.</summary>
    private static bool Assign(List<(Sample Sample, double[] Vector)> points, double[][] centers, int[] assignment)
    {
        bool changed = false;
        for (int i = 0; i < points.Count; i++)
        {
            int best = 0;
            double bestDistance = double.MaxValue;
            for (int c = 0; c < centers.Length; c++)
            {
                double distance = SquaredDistance(points[i].Vector, centers[c]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = c;
                }
            }

            if (assignment[i] != best)
            {
                assignment[i] = best;
                changed = true;
            }
        }

        return changed;
    }

    ///<summary>
    ///Moves every center to the mean of its members. A center nobody chose is moved onto the sample farthest from its own
    ///center, so no family is lost to an unlucky seed.
    ///</summary>
    private static void Recenter(List<(Sample Sample, double[] Vector)> points, double[][] centers, int[] assignment)
    {
        int dimensions = centers[0].Length;
        double[][] sums = [.. centers.Select(_ => new double[dimensions])];
        int[] counts = new int[centers.Length];
        for (int i = 0; i < points.Count; i++)
        {
            counts[assignment[i]]++;
            for (int d = 0; d < dimensions; d++)
            {
                sums[assignment[i]][d] += points[i].Vector[d];
            }
        }

        for (int c = 0; c < centers.Length; c++)
        {
            if (counts[c] > 0)
            {
                for (int d = 0; d < dimensions; d++)
                {
                    centers[c][d] = sums[c][d] / counts[c];
                }

                continue;
            }

            int farthest = 0;
            double farthestDistance = -1;
            for (int i = 0; i < points.Count; i++)
            {
                double distance = SquaredDistance(points[i].Vector, centers[assignment[i]]);
                if (distance > farthestDistance)
                {
                    farthestDistance = distance;
                    farthest = i;
                }
            }

            centers[c] = [.. points[farthest].Vector];
            assignment[farthest] = c;
        }
    }

    private static SoundFamily Describe(List<(Sample Sample, double[] Vector)> members, double[] center)
    {
        Sample medoid = members
            .OrderBy(member => SquaredDistance(member.Vector, center))
            .ThenBy(member => member.Sample.Name, StringComparer.OrdinalIgnoreCase)
            .First().Sample;

        // Counted by instrument family (kick, snare, hi-hat ...) rather than by exact instrument, so a cluster of hi-hats that is
        // half closed and half open is one family of hi-hats and not two minorities.
        IGrouping<string, Sample> dominant = members
            .Select(member => member.Sample)
            .GroupBy(FamilyKey, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .First();
        _ = Taxonomies.Instruments.TryGet(dominant.Key, out InstrumentConcept? concept);
        double purity = (double)dominant.Count() / members.Count;

        return new SoundFamily(medoid, concept, purity, [.. members.Select(member => member.Sample).OrderBy(sample => sample.Name, StringComparer.OrdinalIgnoreCase)]);
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Clusters the samples that have a fingerprint (the rest belong to no family), largest family first.
    ///</summary>
    ///<param name="samples">The samples, in a stable order.</param>
    ///<param name="space">Their fingerprint space.</param>
    ///<param name="familyCount">Number of families, or null for about sqrt(n / 2).</param>
    internal static IReadOnlyList<SoundFamily> Cluster(IReadOnlyList<Sample> samples, FingerprintSpace space, int? familyCount = null)
    {
        List<(Sample Sample, double[] Vector)> points = [];
        foreach (Sample sample in samples)
        {
            double[]? vector = space.Vector(sample);
            if (vector is not null)
            {
                points.Add((sample, vector));
            }
        }

        if (points.Count == 0)
        {
            return [];
        }

        int k = Math.Clamp(familyCount ?? DefaultCount(points.Count), 1, points.Count);
        double[][] centers = SeedCenters(points, k);
        int[] assignment = new int[points.Count];
        Array.Fill(assignment, -1);
        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            bool changed = Assign(points, centers, assignment);
            if (!changed && iteration > 0)
            {
                break;
            }

            Recenter(points, centers, assignment);
        }

        List<SoundFamily> families = [];
        for (int c = 0; c < k; c++)
        {
            List<(Sample Sample, double[] Vector)> members = [.. points.Where((_, i) => assignment[i] == c)];
            if (members.Count > 0)
            {
                families.Add(Describe(members, centers[c]));
            }
        }

        return [.. families.OrderByDescending(family => family.Members.Count).ThenBy(family => family.Name, StringComparer.OrdinalIgnoreCase)];
    }
    #endregion
}
