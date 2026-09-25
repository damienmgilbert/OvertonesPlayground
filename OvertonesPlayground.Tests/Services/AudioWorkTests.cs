using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

///<summary>
///The audio services must keep their number-crunching off the UI thread. A UI-like <see cref="SynchronizationContext"/>
///stands in for it: if a service ever resumed on it part-way through (an await without <see cref="AudioWork"/> around it),
///the context would be posted to.
///</summary>
public sealed class AudioWorkTests : IDisposable
{
    #region Constants
    private const int Rate = 44_100;
    #endregion

    #region Fields
    private readonly TempFileSystem _files = new();
    #endregion

    #region Private methods
    ///<summary>
    ///Starts <paramref name="start"/> with <paramref name="context"/> as the current context, as if called from the UI thread,
    ///and puts the previous context back before returning so only the started call captures it.
    ///</summary>
    private static Task<T> StartUnder<T>(SynchronizationContext context, Func<Task<T>> start)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            return start();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private async Task<string> SourceClipAsync(string name) => await WavTestFiles.WriteAsync(_files.InAppData("clips", name), TestSignals.Sine(440, Rate, 0.5, amplitude: 0.3), 1, Rate);
    #endregion

    #region Public methods
    public void Dispose() => _files.Dispose();

    [Fact]
    public async Task Control_AnAwaitOutsideAudioWork_DoesResumeOnTheContext()
    {
        CountingContext context = new();

        await StartUnder(context, async () =>
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            return 0;
        });

        Assert.True(context.Posts > 0);
    }

    [Fact]
    public async Task Editor_AnEffect_NeverResumesOnTheCallersContext()
    {
        string source = await SourceClipAsync("in.wav");
        CountingContext context = new();
        AudioEditorService service = new(_files);

        string output = await StartUnder(context, () => service.ApplyGainAsync(source, 6, "louder"));

        Assert.True(File.Exists(output));
        Assert.Equal(0, context.Posts);
    }

    [Fact]
    public async Task Editor_WaveformPeaks_NeverResumeOnTheCallersContext()
    {
        string source = await SourceClipAsync("in.wav");
        CountingContext context = new();
        AudioEditorService service = new(_files);

        float[] peaks = await StartUnder(context, () => service.GetWaveformPeaksAsync(source, 50));

        Assert.Equal(50, peaks.Length);
        Assert.Equal(0, context.Posts);
    }

    [Fact]
    public async Task Mixdown_Render_NeverResumesOnTheCallersContext()
    {
        string clip = await SourceClipAsync("clip.wav");
        Track track = new() { Name = "Track", Volume = 1 };
        track.Clips.Add(new TrackClip { ClipFilePath = clip, ClipName = "Clip" });
        MixdownService service = new(_files);
        CountingContext context = new();

        string output = await StartUnder(context, () => service.RenderAsync(new MixProject { Name = "Mix", Tracks = [track] }, "mix"));

        Assert.True(File.Exists(output));
        Assert.Equal(0, context.Posts);
    }

    [Fact]
    public async Task RunAsync_RunsTheWorkOnAnotherThread()
    {
        int caller = Environment.CurrentManagedThreadId;

        int worker = await AudioWork.RunAsync(() => Environment.CurrentManagedThreadId);
        int asyncWorker = await AudioWork.RunAsync(async () =>
        {
            await Task.Yield();
            return Environment.CurrentManagedThreadId;
        });

        Assert.NotEqual(caller, worker);
        Assert.NotEqual(caller, asyncWorker);
    }

    [Fact]
    public async Task Synthesis_Tone_NeverResumesOnTheCallersContext()
    {
        SoundSynthesisService service = new(_files);
        CountingContext context = new();

        AudioClip clip = await StartUnder(context, () => service.GenerateToneAsync(WaveformType.Sine, 440, 0.2, 0.5, "Tone"));

        Assert.True(File.Exists(clip.FilePath));
        Assert.Equal(0, context.Posts);
    }

    [Fact]
    public async Task WavFile_ReadAndWrite_NeverResumeOnTheCallersContext()
    {
        string path = _files.InAppData("clips", "roundtrip.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WavFile wav = new() { Channels = 1, SampleRate = Rate, BitsPerSample = 16, Samples = TestSignals.Sine(440, Rate, 0.1, amplitude: 0.3) };
        CountingContext context = new();

        WavFile read = await StartUnder(context, async () =>
        {
            await wav.WriteAsync(path).ConfigureAwait(false);
            return await WavFile.ReadAsync(path).ConfigureAwait(false);
        });

        Assert.Equal(wav.Samples, read.Samples);
        Assert.Equal(0, context.Posts);
    }
    #endregion

    private sealed class CountingContext : SynchronizationContext
    {
        #region Fields
        private int _posts;
        #endregion

        #region Public methods
        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _posts);
            base.Post(d, state);
        }
        #endregion

        #region Public properties
        public int Posts => Volatile.Read(ref _posts);
        #endregion
    }
}
