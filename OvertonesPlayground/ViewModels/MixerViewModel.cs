using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the mixer page. Manages a collection of mixer channel view models and provides commands to load
///samples and control playback per channel.
///</summary>
public partial class MixerViewModel : BaseViewModel
{
    #region Fields

    ///<summary>
    ///Colors assigned round-robin to channel strips, matching the palette used by the Launchpad so track colors read
    ///consistently across the app.
    ///</summary>
    private static readonly string[] ChannelPalette = ["#512BD4", "#D600AA", "#2B9348", "#F77F00", "#0077B6", "#9D4EDD", "#E5383B", "#FFB703",];
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the view model and populates it with four empty channel strips.
    ///</summary>
    public MixerViewModel(IAudioPlaybackService playbackService, IAudioLibraryService libraryService, ILoggerFactory loggerFactory, ILogger<MixerViewModel> logger) : base(logger)
    {
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Mixer";

        for (int i = 1; i <= 4; i++)
        {
            MixerChannelStrip strip = new() { Name = $"Track {i}", ColorHex = ChannelPalette[(i - 1) % ChannelPalette.Length] };
            MixerChannelViewModel channel = new(strip, _playbackService, loggerFactory.CreateLogger<MixerChannelViewModel>()) { Number = i };
            channel.PropertyChanged += OnChannelPropertyChanged;
            Channels.Add(channel);
        }
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Opens the file picker and assigns the chosen sample to a channel (without starting playback).
    ///</summary>
    [RelayCommand]
    private async Task LoadSampleAsync(MixerChannelViewModel? channel)
    {
        if (channel is null)
        {
            return;
        }

        AudioClip? clip = await _libraryService.ImportFromPickerAsync();
        if (clip is not null)
        {
            Log_LoadedSample(clip.Name, channel.Name);
            channel.AssignSource(clip.FilePath, clip.Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded sample '{ClipName}' into channel '{ChannelName}'.")]
    private partial void Log_LoadedSample(string clipName, string channelName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Solo state changed; {SoloedCount} channel(s) soloed.")]
    private partial void Log_SoloStateChanged(int soloedCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Stopping all channels.")]
    private partial void Log_StoppingAllChannels();

    ///<summary>
    ///Re-evaluates solo whenever any channel's solo state changes: soloing one channel dims and silences the others,
    ///matching Ableton/Audacity mixer behavior.
    ///</summary>
    private void OnChannelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MixerChannelViewModel.IsSoloed))
        {
            return;
        }

        bool anySoloed = Channels.Any(c => c.IsSoloed);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            Log_SoloStateChanged(Channels.Count(c => c.IsSoloed));
        }
        foreach (MixerChannelViewModel channel in Channels)
        {
            channel.IsDimmed = anySoloed && !channel.IsSoloed;
        }
    }

    ///<summary>
    ///Stops every channel's voice.
    ///</summary>
    [RelayCommand]
    private void StopAll()
    {
        Log_StoppingAllChannels();
        foreach (MixerChannelViewModel channel in Channels)
        {
            channel.Stop();
        }
    }

    ///<summary>
    ///Stops a single channel's voice.
    ///</summary>
    [RelayCommand]
    private static void StopChannel(MixerChannelViewModel? channel) => channel?.Stop();
    #endregion

    #region Public properties
    ///<summary>
    ///Collection of mixer channel view models shown in the UI.
    ///</summary>
    public ObservableCollection<MixerChannelViewModel> Channels { get; } = [];
    #endregion
}
