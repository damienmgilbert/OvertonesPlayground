using System.Text;

namespace OvertonesPlayground.Ontology.Theory;

///<summary>
///How a bar is divided: beats in a bar and steps in a beat. 4/4 in sixteenth notes is four beats of four steps.
///</summary>
///<param name="BeatsPerBar">Beats in a bar.</param>
///<param name="StepsPerBeat">Sequencer steps in a beat (4 for sixteenth notes, 3 for triplet eighths).</param>
public sealed record Meter(int BeatsPerBar, int StepsPerBeat)
{
    #region Public methods
    ///<summary>Whether step <paramref name="step"/> falls on a beat.</summary>
    public bool IsOnBeat(int step) => step % StepsPerBeat == 0;

    ///<summary>Whether step <paramref name="step"/> is the first of a bar.</summary>
    public bool IsDownbeat(int step) => step % StepsPerBar == 0;

    ///<summary>
    ///Whether step <paramref name="step"/> is a backbeat: beat two or four of a four-beat bar (the snare's spots).
    ///</summary>
    public bool IsBackbeat(int step) => IsOnBeat(step) && BeatsPerBar % 2 == 0 && (step / StepsPerBeat) % 2 == 1;

    ///<summary>
    ///Whether step <paramref name="step"/> falls between the eighth notes (the "e" and "a" of "1 e and a").
    ///</summary>
    public bool IsOffEighth(int step) => StepsPerBeat == 4 && step % 2 == 1;
    #endregion

    #region Public properties
    ///<summary>Steps in a bar.</summary>
    public int StepsPerBar => BeatsPerBar * StepsPerBeat;

    ///<summary>Four beats of sixteenth notes: almost all dance, pop and hip-hop.</summary>
    public static Meter Common { get; } = new(4, 4);
    #endregion
}

///<summary>
///One step of a <see cref="StepPattern"/>.
///</summary>
///<param name="IsOn">Whether the step plays.</param>
///<param name="Velocity">How hard, 0 to 1.</param>
///<param name="Probability">Chance, in percent, that it plays each time round.</param>
public readonly record struct StepHit(bool IsOn, double Velocity, int Probability)
{
    ///<summary>A rest.</summary>
    public static StepHit Rest { get; } = new(false, 0, 100);
}

///<summary>
///A drum or note pattern written one character per step: <c>X</c>, <c>x</c>, <c>o</c> and <c>-</c> are hits from loud to quiet,
///<c>?</c> a hit that plays half the time and <c>.</c> a rest. <c>"X...x...X...x..."</c> is a kick on every beat. Patterns can
///be measured (how busy, how syncopated) and varied by rules that keep what makes them recognisable.
///</summary>
public sealed class StepPattern : IEquatable<StepPattern>
{
    #region Fields
    private readonly StepHit[] _steps;
    #endregion

    #region Constructors
    private StepPattern(StepHit[] steps) => _steps = steps;
    #endregion

    #region Private methods
    private static StepHit Decode(char step) => step switch
    {
        'X' => new StepHit(true, 1.0, 100),
        'x' => new StepHit(true, 0.75, 100),
        'o' => new StepHit(true, 0.5, 100),
        '-' => new StepHit(true, 0.25, 100),
        '?' => new StepHit(true, 0.5, 50),
        '.' => StepHit.Rest,
        _ => throw new FormatException($"'{step}' is not a step: use X, x, o, - (loud to quiet), ? (half the time) or . (rest)."),
    };

    private static char Encode(StepHit hit) => hit switch
    {
        { IsOn: false } => '.',
        { Probability: < 100 } => '?',
        { Velocity: >= 0.9 } => 'X',
        { Velocity: >= 0.65 } => 'x',
        { Velocity: >= 0.4 } => 'o',
        _ => '-',
    };

    private StepPattern With(Action<StepHit[]> change)
    {
        StepHit[] copy = [.. _steps];
        change(copy);
        return new StepPattern(copy);
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Reads a pattern (see the type's summary).
    ///</summary>
    ///<exception cref="FormatException">A character is not a step.</exception>
    public static StepPattern Parse(string steps)
    {
        ArgumentException.ThrowIfNullOrEmpty(steps);
        return new StepPattern([.. steps.Select(Decode)]);
    }

    ///<summary>
    ///A pattern of <paramref name="length"/> rests.
    ///</summary>
    public static StepPattern Empty(int length) => new([.. Enumerable.Repeat(StepHit.Rest, length)]);

    ///<summary>
    ///The pattern with its steps moved <paramref name="steps"/> later, wrapping round (earlier if negative).
    ///</summary>
    public StepPattern Rotate(int steps)
    {
        int n = _steps.Length;
        return new StepPattern([.. Enumerable.Range(0, n).Select(i => _steps[(((i - steps) % n) + n) % n])]);
    }

    ///<summary>
    ///The pattern repeated to <paramref name="length"/> steps (or cut to it).
    ///</summary>
    public StepPattern Fit(int length) => new([.. Enumerable.Range(0, length).Select(i => _steps[i % _steps.Length])]);

    ///<summary>
    ///A variation for a busier section: quiet ghost notes added next to existing hits, never on a strong beat and never more than
    ///<paramref name="maxAdded"/> of them.
    ///</summary>
    public StepPattern AddGhosts(Random random, Meter meter, int maxAdded, double chance = 0.35) => With(steps =>
    {
        int added = 0;
        for (int i = 0; i < steps.Length && added < maxAdded; i++)
        {
            bool isFree = !steps[i].IsOn && !meter.IsOnBeat(i);
            bool isNextToHit = steps[(i + steps.Length - 1) % steps.Length].IsOn || steps[(i + 1) % steps.Length].IsOn;
            if (isFree && isNextToHit && random.NextDouble() < chance)
            {
                steps[i] = new StepHit(true, 0.25, 100);
                added++;
            }
        }
    });

    ///<summary>
    ///A sparser variation for a breakdown: keeps the hits on the beat (and every loud hit on a downbeat), and keeps each other hit
    ///only with probability <paramref name="keep"/>.
    ///</summary>
    public StepPattern Thin(Random random, Meter meter, double keep = 0.4) => With(steps =>
    {
        for (int i = 0; i < steps.Length; i++)
        {
            bool isAnchor = meter.IsDownbeat(i) || (meter.IsOnBeat(i) && steps[i].Velocity >= 0.9);
            if (steps[i].IsOn && !isAnchor && random.NextDouble() >= keep)
            {
                steps[i] = StepHit.Rest;
            }
        }
    });

    ///<summary>
    ///A humanised variation: some quiet off-beat hits become chance hits (they play half the time), so repeats of the pattern
    ///differ slightly. The loud hits that carry the groove are never touched.
    ///</summary>
    public StepPattern Loosen(Random random, Meter meter, double chance = 0.2) => With(steps =>
    {
        for (int i = 0; i < steps.Length; i++)
        {
            bool isLoose = steps[i].IsOn && steps[i].Velocity < 0.9 && !meter.IsOnBeat(i);
            if (isLoose && random.NextDouble() < chance)
            {
                steps[i] = steps[i] with { Probability = 50 };
            }
        }
    });

    ///<summary>
    ///A variation for the end of a phrase: the last beat filled with a run of hits that rise in loudness.
    ///</summary>
    public StepPattern Fill(Meter meter)
    {
        int start = Math.Max(0, _steps.Length - meter.StepsPerBeat);
        return With(steps =>
        {
            for (int i = start; i < steps.Length; i++)
            {
                double velocity = 0.5 + (0.5 * (i - start + 1) / (steps.Length - start));
                steps[i] = new StepHit(true, Math.Round(velocity, 2), 100);
            }
        });
    }

    ///<summary>
    ///One hit moved a step earlier or later, if it is not on a beat: small displacements are how drummers vary a groove without
    ///losing it.
    ///</summary>
    public StepPattern Nudge(Random random, Meter meter) => With(steps =>
    {
        List<int> movable = [.. Enumerable.Range(0, steps.Length).Where(i => steps[i].IsOn && !meter.IsOnBeat(i))];
        if (movable.Count == 0)
        {
            return;
        }

        int from = movable[random.Next(movable.Count)];
        int to = from + (random.Next(2) == 0 ? -1 : 1);
        if (to < 0 || to >= steps.Length || steps[to].IsOn || meter.IsOnBeat(to))
        {
            return;
        }

        steps[to] = steps[from];
        steps[from] = StepHit.Rest;
    });

    ///<inheritdoc/>
    public bool Equals(StepPattern? other) => other is not null && _steps.AsSpan().SequenceEqual(other._steps);

    ///<inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StepPattern);

    ///<inheritdoc/>
    public override int GetHashCode() => ToString().GetHashCode(StringComparison.Ordinal);

    ///<summary>
    ///The pattern written one character per step.
    ///</summary>
    public override string ToString()
    {
        StringBuilder text = new(_steps.Length);
        foreach (StepHit hit in _steps)
        {
            _ = text.Append(Encode(hit));
        }

        return text.ToString();
    }

    ///<summary>
    ///Share of the hits that fall between the eighth notes or off the beat, weighted by how loud they are: 0 for a straight
    ///pattern, towards 1 for a heavily syncopated one.
    ///</summary>
    public double Syncopation(Meter meter)
    {
        double total = _steps.Where(hit => hit.IsOn).Sum(hit => hit.Velocity);
        if (total <= 0)
        {
            return 0;
        }

        double off = _steps.Select((hit, i) => (hit, i)).Where(p => p.hit.IsOn && !meter.IsOnBeat(p.i)).Sum(p => p.hit.Velocity * (meter.IsOffEighth(p.i) ? 1.0 : 0.5));
        return off / total;
    }
    #endregion

    #region Public properties
    ///<summary>The steps.</summary>
    public IReadOnlyList<StepHit> Steps => _steps;

    ///<summary>Number of steps.</summary>
    public int Length => _steps.Length;

    ///<summary>Number of steps that play.</summary>
    public int HitCount => _steps.Count(hit => hit.IsOn);

    ///<summary>Share of the steps that play, 0 to 1.</summary>
    public double Density => _steps.Length == 0 ? 0 : HitCount / (double)_steps.Length;
    #endregion
}
