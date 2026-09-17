using System.Diagnostics;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;
using Plugin.Maui.Audio;

namespace OvertonesPlayground.Services.Implementations;

public class AudioRecorderService : IAudioRecorderService, IDisposable
{
    private readonly IAudioManager _audioManager;
    private readonly IAudioRecorder _recorder;
    private readonly Stopwatch _stopwatch = new();
    private readonly System.Timers.Timer _tickTimer;
    private string? _currentFilePath;

    public AudioRecorderService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
        _recorder = _audioManager.CreateRecorder();

        _tickTimer = new System.Timers.Timer(100);
        _tickTimer.Elapsed += (_, _) => ElapsedChanged?.Invoke(this, Elapsed);
    }

    public bool IsRecording => _recorder.IsRecording;

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public event EventHandler<TimeSpan>? ElapsedChanged;

    public async Task StartAsync()
    {
        var directory = Path.Combine(FileSystem.AppDataDirectory, "Clips");
        Directory.CreateDirectory(directory);
        _currentFilePath = Path.Combine(directory, $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        await _recorder.StartAsync(_currentFilePath, new AudioRecorderOptions
        {
            Encoding = Plugin.Maui.Audio.Encoding.Wav,
            SampleRate = 44_100,
            Channels = ChannelType.Mono,
            BitDepth = BitDepth.Pcm16bit,
        });

        _stopwatch.Restart();
        _tickTimer.Start();
    }

    public async Task<AudioClip> StopAsync(string name)
    {
        var source = await _recorder.StopAsync();
        _stopwatch.Stop();
        _tickTimer.Stop();

        var path = source is FileAudioSource fileSource ? fileSource.GetFilePath() : _currentFilePath ?? string.Empty;

        return new AudioClip
        {
            Name = name,
            FilePath = path,
            Duration = _stopwatch.Elapsed,
            IsUserRecording = true,
        };
    }

    public async Task CancelAsync()
    {
        if (_recorder.IsRecording)
        {
            await _recorder.StopAsync();
        }

        _stopwatch.Reset();
        _tickTimer.Stop();

        if (_currentFilePath is not null && File.Exists(_currentFilePath))
        {
            File.Delete(_currentFilePath);
        }
    }

    public void Dispose()
    {
        _tickTimer.Dispose();
        (_recorder as IDisposable)?.Dispose();
    }
}
