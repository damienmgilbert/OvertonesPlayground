using System.Globalization;

namespace SampleAnalyzer;

///<summary>
///Implements <c>report</c>: everything a maintainer needs to judge and improve the catalog, measured rather than
///assumed - coverage, disagreements between file names and audio, and sanity checks that the acoustic facets behave.
///</summary>
internal static class ReportCommand
{
    #region Private methods
    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = [.. values.Order()];
        return sorted.Length == 0 ? double.NaN : sorted[sorted.Length / 2];
    }

    private static string Percent(int part, int whole) => whole == 0 ? "n/a" : (100.0 * part / whole).ToString("0.0", CultureInfo.InvariantCulture) + " %";

    private static void Heading(string title) => Console.WriteLine($"\n== {title} ==");

    private static void PrintCounts<TKey>(IEnumerable<TKey> keys, string label)
        where TKey : notnull
    {
        Console.WriteLine($"{label}:");
        foreach (IGrouping<TKey, TKey> group in keys.GroupBy(key => key).OrderByDescending(group => group.Count()))
        {
            Console.WriteLine($"  {group.Key,-22} {group.Count(),5}");
        }
    }

    private static InstrumentConcept? Instrument(Sample sample) =>
        Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept) ? concept : null;
    #endregion

    #region Public methods
    internal static void PrintSummary(SampleCatalog catalog)
    {
        IReadOnlyList<Sample> samples = catalog.Samples;
        int unclassified = samples.Count(sample => sample.Classification.Instrument.Value == "unclassified");
        int review = samples.Count(sample => sample.Classification.NeedsReview);
        Console.WriteLine($"Summary: {samples.Count} samples; {samples.Count(s => s.Status == AnalysisStatus.Analyzed)} analysed, "
            + $"{samples.Count(s => s.Status == AnalysisStatus.Partial)} partial, {samples.Count(s => s.Status == AnalysisStatus.UnsupportedFormat)} unsupported, "
            + $"{samples.Count(s => s.Status == AnalysisStatus.Failed)} failed; {unclassified} unclassified ({Percent(unclassified, samples.Count)}), "
            + $"{review} flagged for review ({Percent(review, samples.Count)}).");
    }

    internal static int Run(string catalogPath, Dictionary<string, string> options)
    {
        using FileStream stream = File.OpenRead(catalogPath);
        SampleCatalog catalog = SampleCatalogSerializer.Deserialize(stream);
        IReadOnlyList<Sample> samples = catalog.Samples;
        int reviewLimit = options.TryGetValue("review", out string? r) ? int.Parse(r, CultureInfo.InvariantCulture) : 40;

        Heading("Coverage");
        Console.WriteLine($"catalog {catalogPath}: schema {catalog.SchemaVersion}, analyzer {catalog.AnalyzerVersion}");
        PrintSummary(catalog);
        PrintCounts(samples.Select(sample => sample.Status), "status");
        foreach (Sample sample in samples.Where(s => s.Notes is { Count: > 0 }).Take(15))
        {
            Console.WriteLine($"  note  {sample.Id}: {string.Join("; ", sample.Notes!)}");
        }

        int notes = samples.Count(s => s.Notes is { Count: > 0 });
        Console.WriteLine($"  {notes} samples carry analysis notes ({samples.Count(s => s.Technical?.WasTruncated == true)} truncated data chunks).");

        Heading("Instrument taxonomy");
        PrintCounts(samples.Select(sample => Instrument(sample)?.Category.DisplayName ?? "?"), "category");
        Console.WriteLine("family (depth 2):");
        foreach (IGrouping<string, Sample> group in samples.GroupBy(sample => Instrument(sample)?.Family.Key ?? "?").OrderByDescending(g => g.Count()).Take(40))
        {
            Console.WriteLine($"  {group.Key,-24} {group.Count(),5}");
        }

        Heading("Content type / origin / kit / style");
        PrintCounts(samples.Select(sample => sample.Classification.ContentType.Value), "content type");
        PrintCounts(samples.Select(sample => sample.Classification.Origin.Value), "origin");
        PrintCounts(samples.Select(sample => sample.Classification.Kit?.Value ?? "(none)"), "kit");
        PrintCounts(samples.Select(sample => sample.Classification.Style?.Value ?? "(none)"), "style");

        Heading("Audio versus names (leave-one-out nearest-neighbour vote on the acoustic fingerprint)");
        SignalEvaluation evaluation = CorpusClassifier.EvaluateSignalAgreement(samples);
        Console.WriteLine($"evaluated {evaluation.Evaluated} confidently named samples: {evaluation.Agreed} agree = {evaluation.Accuracy:P1}");
        Console.WriteLine($"of those, the distinctive kit families (kick, snare, hi-hat, tom, cymbal, clap, rim): {evaluation.CoreAgreed}/{evaluation.CoreEvaluated} = {evaluation.CoreAccuracy:P1}");
        Console.WriteLine("per family: samples, share predicted correctly, most common wrong prediction");
        foreach (IGrouping<string, SignalDisagreement> wrong in evaluation.Disagreements.GroupBy(d => d.ActualFamily).OrderByDescending(g => g.Count()).Take(14))
        {
            int total = samples.Count(sample => Instrument(sample)?.Family.Key == wrong.Key && sample.Classification.Instrument.Confidence >= 0.8);
            string confusedWith = string.Join(", ", wrong.GroupBy(d => d.PredictedFamily).OrderByDescending(g => g.Count()).Take(3).Select(g => $"{g.Key} x{g.Count()}"));
            Console.WriteLine($"  {wrong.Key,-24} {total,5}  {(total - wrong.Count()) * 100.0 / Math.Max(1, total),5:0}% right   confused with {confusedWith}");
        }

        foreach (SignalDisagreement d in evaluation.Disagreements.Take(25))
        {
            Console.WriteLine($"  {d.Sample.Id,-44} name says {d.ActualFamily,-20} audio says {d.PredictedFamily,-20} ({d.Confidence:0.00})");
        }

        Heading("Do the acoustic facets behave? (median per family)");
        Console.WriteLine($"{"family",-24} {"n",5} {"centroid",9} {"flatness",9} {"dur s",7} {"LUFS",7} {"attack ms",10} {"decay ms",9} {"pitched",8} {"mono",6}");
        foreach (IGrouping<string, Sample> group in samples.Where(s => s.Spectral is not null && s.Dynamics is not null)
            .GroupBy(sample => Instrument(sample)?.Family.Key ?? "?").Where(g => g.Count() >= 8).OrderByDescending(g => g.Count()).Take(22))
        {
            int pitched = group.Count(s => s.Tonality?.Character.HasFlag(TonalCharacter.Pitched) == true);
            int mono = group.Count(s => s.Stereo?.Image is StereoImage.Mono or StereoImage.DualMono);
            Console.WriteLine($"{group.Key,-24} {group.Count(),5} {Median(group.Select(s => s.Spectral!.CentroidHz)),9:0} {Median(group.Select(s => s.Spectral!.Flatness)),9:0.00} "
                + $"{Median(group.Select(s => s.Technical!.DurationSeconds)),7:0.00} {Median(group.Select(s => s.Dynamics!.IntegratedLufs)),7:0.0} "
                + $"{Median(group.Select(s => s.Dynamics!.AttackMs)),10:0.0} {Median(group.Select(s => s.Dynamics!.DecayMs)),9:0} {Percent(pitched, group.Count()),8} {Percent(mono, group.Count()),6}");
        }

        Heading("Facet distributions");
        PrintCounts(samples.Where(s => s.Stereo is not null).Select(s => s.Stereo!.Image), "stereo image");
        PrintCounts(samples.Where(s => s.Dynamics is not null).Select(s => s.Dynamics!.Shape), "envelope shape");
        PrintCounts(samples.Where(s => s.Dynamics is not null).Select(s => s.Dynamics!.Loudness), "loudness class");
        PrintCounts(samples.Where(s => s.Technical is not null).Select(s => s.Technical!.Length), "length class");
        Console.WriteLine("tonal character flags:");
        foreach (TonalCharacter flag in Enum.GetValues<TonalCharacter>().Where(f => f != TonalCharacter.None))
        {
            Console.WriteLine($"  {flag,-22} {samples.Count(s => s.Tonality?.Character.HasFlag(flag) == true),5}");
        }

        int clipped = samples.Count(s => s.Dynamics?.ClippedSamples > 0);
        int highDc = samples.Count(s => Math.Abs(s.Dynamics?.DcOffset ?? 0) > 0.01);
        int badMono = samples.Count(s => s.Stereo?.MonoCompatibilityDb < -6);
        Console.WriteLine($"clipped: {clipped}; DC offset > 1 %: {highDc}; mono-sum loses more than 6 dB: {badMono}; out of phase: {samples.Count(s => s.Stereo?.Image == StereoImage.OutOfPhase)}");

        Heading("Tempo: name versus detected");
        List<Sample> named = [.. samples.Where(s => s.Classification.Attributes.TempoBpm is not null && s.Rhythm?.DetectedBpm is not null)];
        int agree = named.Count(s => s.Rhythm!.TempoAgreesWithName == true);
        Console.WriteLine($"{named.Count} files with both: {agree} agree ({Percent(agree, named.Count)}), half/double time and dotted/triplet relations allowed");
        foreach (Sample s in named.Where(s => s.Rhythm!.TempoAgreesWithName == false).Take(12))
        {
            Console.WriteLine($"  {s.Id,-44} name {s.Classification.Attributes.TempoBpm,5:0}  detected {s.Rhythm!.DetectedBpm,6:0.0}  (conf {s.Rhythm.TempoConfidence:0.00})");
        }

        Console.WriteLine($"loop-like: {samples.Count(s => s.Rhythm?.IsLoopLike == true)}; files with a rhythm facet: {samples.Count(s => s.Rhythm is not null)}");

        Heading("Key: name versus detected pitch");
        List<Sample> keyed = [.. samples.Where(s => s.Classification.Attributes.KeyPitchClass is not null && s.Tonality?.PitchClass is not null && s.Tonality.PitchConfidence >= 0.85)];
        int keyAgree = keyed.Count(s => s.Classification.Attributes.KeyPitchClass == s.Tonality!.PitchClass);
        Console.WriteLine($"{keyed.Count} files with a named key and a confident pitch: {keyAgree} agree on pitch class ({Percent(keyAgree, keyed.Count)})");
        IEnumerable<int> octaveDeltas = keyed
            .Where(s => s.Classification.Attributes.Octave is not null && s.Classification.Attributes.KeyPitchClass == s.Tonality!.PitchClass)
            .Select(s => ((int)Math.Round(s.Tonality!.MidiNote!.Value) / 12) - 1 - s.Classification.Attributes.Octave!.Value);
        PrintCounts(octaveDeltas.Select(delta => $"measured - named octave = {delta}"), "octave convention (files that name an octave)");
        foreach (Sample s in keyed.Where(s => s.Classification.Attributes.KeyPitchClass != s.Tonality!.PitchClass).Take(12))
        {
            Console.WriteLine($"  {s.Id,-44} name {s.Classification.Attributes.KeyName(),-8} measured {s.Tonality!.NoteName} ({s.Tonality.FundamentalHz:0.0} Hz, conf {s.Tonality.PitchConfidence:0.00})");
        }

        Heading($"Flagged for review (first {reviewLimit})");
        foreach (Sample s in samples.Where(s => s.Classification.NeedsReview).OrderBy(s => s.Classification.Instrument.Confidence).Take(reviewLimit))
        {
            Console.WriteLine($"  {s.Id,-44} {Instrument(s)?.Path,-34} {string.Join(" | ", s.Classification.ReviewReasons)}");
        }

        Heading("Names with no instrument word (first 40)");
        foreach (Sample s in samples.Where(s => s.Classification.Instrument.Value == "unclassified" || s.Classification.Instrument.Evidence.All(e => e.Source != EvidenceSource.Filename)).Take(40))
        {
            Console.WriteLine($"  {s.Id,-44} -> {Instrument(s)?.Path} ({s.Classification.Instrument.Confidence:0.00}) {string.Join("; ", s.Classification.Instrument.Evidence.Select(e => e.Detail))}");
        }

        return 0;
    }
    #endregion
}
