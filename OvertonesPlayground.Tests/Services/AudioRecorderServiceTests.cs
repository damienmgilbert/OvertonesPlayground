using OvertonesPlayground.Services.Implementations;
using Plugin.Maui.Audio;

namespace OvertonesPlayground.Tests.Services;

public sealed class AudioRecorderServiceTests : IDisposable
{
    #region Fields
    private readonly IAudioManager _audioManager = Substitute.For<IAudioManager>();
    private readonly List<AudioRecorderService> _created = [];
    private readonly TempFileSystem _files = new();
    private readonly IAudioRecorder _recorder = Substitute.For<IAudioRecorder, IDisposable>();
    #endregion

    #region Private methods
    private AudioRecorderService Create()
    {
        _audioManager.CreateRecorder().Returns(_recorder);
        AudioRecorderService service = new(_audioManager, _files);
        _created.Add(service);
        return service;
    }

    private string StartedPath()
    {
        NSubstitute.Core.ICall call = _recorder.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IAudioRecorder.StartAsync));
        return (string)call.GetArguments()[0]!;
    }
    #endregion

    #region Public methods
    [Fact]
    public async Task Cancel_NothingEverStarted_IsHarmless()
    {
        await Create().CancelAsync();

        await _recorder.DidNotReceive().StopAsync();
    }

    [Fact]
    public async Task Cancel_RecorderNotRunning_StillDeletesWhatWasLeftBehindWithoutStoppingIt()
    {
        AudioRecorderService service = Create();
        await service.StartAsync();
        string path = StartedPath();
        File.WriteAllText(path, "left over");
        _recorder.IsRecording.Returns(false);

        await service.CancelAsync();

        await _recorder.DidNotReceive().StopAsync();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Cancel_StopsReportingElapsedTime()
    {
        AudioRecorderService service = Create();
        await service.StartAsync();
        _recorder.IsRecording.Returns(true);
        await service.CancelAsync();
        int reports = 0;
        service.ElapsedChanged += (_, _) => Interlocked.Increment(ref reports);

        await Task.Delay(250, TestContext.Current.CancellationToken);

        Assert.Equal(0, reports);
    }

    [Fact]
    public async Task Cancel_WhileRecording_StopsTheRecorderAndDeletesTheFile()
    {
        AudioRecorderService service = Create();
        await service.StartAsync();
        string path = StartedPath();
        File.WriteAllText(path, "partial take");
        _recorder.IsRecording.Returns(true);

        await service.CancelAsync();

        await _recorder.Received(1).StopAsync();
        Assert.False(File.Exists(path));
        Assert.Equal(TimeSpan.Zero, service.Elapsed);
    }

    public void Dispose()
    {
        foreach (AudioRecorderService service in _created)
        {
            service.Dispose();
        }

        _files.Dispose();
    }

    [Fact]
    public void Dispose_ReleasesThePlatformRecorder()
    {
        AudioRecorderService service = Create();

        service.Dispose();

        ((IDisposable)_recorder).Received(1).Dispose();
    }

    [Fact]
    public void Elapsed_BeforeAnythingIsRecorded_IsZero() { Assert.Equal(TimeSpan.Zero, Create().Elapsed); }
    [Fact]
    public async Task Elapsed_IsReportedRepeatedlyWhileRecording()
    {
        AudioRecorderService service = Create();
        int reports = 0;
        TaskCompletionSource enough = new();
        service.ElapsedChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref reports) >= 2)
            {
                enough.TrySetResult();
            }
        };

        await service.StartAsync();

        await enough.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(reports >= 2);
    }

    [Fact]
    public void IsRecording_FollowsTheRecorder()
    {
        AudioRecorderService service = Create();
        _recorder.IsRecording.Returns(false);
        Assert.False(service.IsRecording);

        _recorder.IsRecording.Returns(true);
        Assert.True(service.IsRecording);
    }

    [Fact]
    public async Task Start_AsksForMonoSixteenBitWavAtCdRate()
    {
        AudioRecorderService service = Create();

        await service.StartAsync();

        await _recorder.Received(1).StartAsync(Arg.Any<string>(), Arg.Is<AudioRecorderOptions>(options => options.Encoding == Encoding.Wav && options.SampleRate == 44_100 && options.Channels == ChannelType.Mono && options.BitDepth == BitDepth.Pcm16bit));
    }

    [Fact]
    public async Task Start_RecordsIntoANewWavInTheClipsFolder()
    {
        AudioRecorderService service = Create();

        await service.StartAsync();

        string path = StartedPath();
        Assert.Equal(_files.InAppData("Clips"), Path.GetDirectoryName(path));
        Assert.StartsWith("recording_", Path.GetFileName(path));
        Assert.EndsWith(".wav", path);
        Assert.True(Directory.Exists(_files.InAppData("Clips")));
    }

    [Fact]
    public async Task Start_StartsTheClock()
    {
        AudioRecorderService service = Create();

        await service.StartAsync();
        await Task.Delay(120, TestContext.Current.CancellationToken);

        Assert.True(service.Elapsed >= TimeSpan.FromMilliseconds(100), $"elapsed only {service.Elapsed}");
    }

    [Fact]
    public async Task Stop_RecorderGivesNoFilePath_FallsBackToTheFileItWasToldToWrite()
    {
        AudioRecorderService service = Create();
        await service.StartAsync();
        _recorder.StopAsync().Returns(Substitute.For<IAudioSource>());

        AudioClip clip = await service.StopAsync("My take");

        Assert.Equal(StartedPath(), clip.FilePath);
    }

    [Fact]
    public async Task Stop_ReturnsAClipForWhereTheRecorderSaidItWrote()
    {
        AudioRecorderService service = Create();
        await service.StartAsync();
        _recorder.StopAsync().Returns(new FileAudioSource("/clips/take.wav"));
        await Task.Delay(30, TestContext.Current.CancellationToken);

        AudioClip clip = await service.StopAsync("My take");

        Assert.Equal("My take", clip.Name);
        Assert.Equal("/clips/take.wav", clip.FilePath);
        Assert.True(clip.IsUserRecording);
        Assert.True(clip.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task Stop_StopsReportingElapsedTime()
    {
        AudioRecorderService service = Create();
        await service.StartAsync();
        _recorder.StopAsync().Returns(Substitute.For<IAudioSource>());
        await service.StopAsync("x");
        int reports = 0;
        service.ElapsedChanged += (_, _) => Interlocked.Increment(ref reports);

        await Task.Delay(250, TestContext.Current.CancellationToken);

        Assert.Equal(0, reports);
    }

    [Fact]
    public async Task Stop_StopsTheClock()
    {
        AudioRecorderService service = Create();
        await service.StartAsync();
        _recorder.StopAsync().Returns(Substitute.For<IAudioSource>());
        await service.StopAsync("x");
        TimeSpan atStop = service.Elapsed;

        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(atStop, service.Elapsed);
    }
    #endregion
}
