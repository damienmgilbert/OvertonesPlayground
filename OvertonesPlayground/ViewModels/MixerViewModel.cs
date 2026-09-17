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
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the view model and populates it with four empty channel strips.
    ///</summary>
    public MixerViewModel(IAudioPlaybackService playbackService, IAudioLibraryService libraryService)
    {
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Mixer";

        for(int i = 1; i <= 4; i++)
        {
            MixerChannelStrip strip = new() { Name = $"Track {i}" };
            Channels.Add(new MixerChannelViewModel(strip, _playbackService));
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
        if(channel is null)
        {
            return;
        }

        AudioClip? clip = await _libraryService.ImportFromPickerAsync();
        if(clip is not null)
        {
            channel.AssignSource(clip.FilePath, clip.Name);
        }
    }

    ///<summary>
    ///Stops every channel's voice.
    ///</summary>
    [RelayCommand]
    private void StopAll()
    {
        foreach(MixerChannelViewModel channel in Channels)
        {
            channel.Stop();
        }
    }

    ///<summary>
    ///Stops a single channel's voice.
    ///</summary>
    [RelayCommand]
    private void StopChannel(MixerChannelViewModel? channel) { channel?.Stop(); }
    #endregion

    #region Public properties
    ///<summary>
    ///Collection of mixer channel view models shown in the UI.
    ///</summary>
    public ObservableCollection<MixerChannelViewModel> Channels { get; } = [];
    #endregion
}
