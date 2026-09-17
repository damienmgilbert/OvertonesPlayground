using System.Diagnostics;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;
using Plugin.Maui.Audio;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="IAudioRecorderService"/>
public class AudioRecorderService : IAudioRecorderService, IDisposable
{
    #region Fields
    private readonly IAudioManager _audioManager;
    private string? _currentFilePath;
    private readonly IAudioRecorder _recorder;
    private readonly Stopwatch _stopwatch = new();
    private readonly System.Timers.Timer _tickTimer;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the recorder service and its underlying platform recorder.
    ///</summary>
    public AudioRecorderService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
        _recorder = _audioManager.CreateRecorder();

        _tickTimer = new System.Timers.Timer(100);
        _tickTimer.Elapsed += (_, _) => ElapsedChanged?.Invoke(this, Elapsed);
    }
    #endregion

    #region Events
    ///<inheritdoc/>
    public event EventHandler<TimeSpan>? ElapsedChanged;
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public async Task CancelAsync()
    {
        if(_recorder.IsRecording)
        {
            await _recorder.StopAsync();
        }

        _stopwatch.Reset();
        _tickTimer.Stop();

        if(_currentFilePath is not null && File.Exists(_currentFilePath))
        {
            File.Delete(_currentFilePath);
        }
    }

    ///<summary>
    ///Releases the tick timer and the underlying platform recorder.
    ///</summary>
    public void Dispose()
    {
        _tickTimer.Dispose();
        (_recorder as IDisposable)?.Dispose();
    }

    ///<inheritdoc/>
    public async Task StartAsync()
    {
        string directory = Path.Combine(FileSystem.AppDataDirectory, "Clips");
        Directory.CreateDirectory(directory);
        _currentFilePath = Path.Combine(directory, $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        await _recorder.StartAsync(_currentFilePath, new AudioRecorderOptions { Encoding = Plugin.Maui.Audio.Encoding.Wav, SampleRate = 44_100, Channels = ChannelType.Mono, BitDepth = BitDepth.Pcm16bit, });

        _stopwatch.Restart();
        _tickTimer.Start();
    }

    ///<inheritdoc/>
    public async Task<AudioClip> StopAsync(string name)
    {
        IAudioSource source = await _recorder.StopAsync();
        _stopwatch.Stop();
        _tickTimer.Stop();

        string path = source is FileAudioSource fileSource ? fileSource.GetFilePath() : _currentFilePath ?? string.Empty;

        return new AudioClip { Name = name, FilePath = path, Duration = _stopwatch.Elapsed, IsUserRecording = true, };
    }
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    ///<inheritdoc/>
    public bool IsRecording => _recorder.IsRecording;
    #endregion
}
