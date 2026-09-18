using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the Text-to-Speech page. "Generate &amp; Play" only previews the synthesized speech; it isn't kept
///until the user explicitly taps Save.
///</summary>
public partial class TextToSpeechViewModel : BaseViewModel
{
    #region Fields
    private readonly IAudioEditorService _editorService;
    private readonly IAudioLibraryService _libraryService;
    ///<summary>
    ///The most recently generated, not-yet-saved preview clip; null once saved or before the first Generate.
    ///</summary>
    private AudioClip? _pendingClip;
    private readonly IAudioPlaybackService _playbackService;
    private readonly ITextToSpeechService _textToSpeechService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the view model.
    ///</summary>
    public TextToSpeechViewModel(ITextToSpeechService textToSpeechService, IAudioEditorService editorService, IAudioPlaybackService playbackService, IAudioLibraryService libraryService, ILogger<TextToSpeechViewModel> logger) : base(logger)
    {
        _textToSpeechService = textToSpeechService;
        _editorService = editorService;
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Text to Speech";
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Gates <see cref="GenerateCommand"/> so it can't run with blank text.
    ///</summary>
    private bool CanGenerate() => !string.IsNullOrWhiteSpace(Text);

    ///<summary>
    ///Synthesizes the current text, previews the result, and enables Save.
    ///</summary>
    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            string name = Text.Length > 40 ? $"{Text[..40]}..." : Text;
            Log_GeneratingSpeech(Text.Length);
            _pendingClip = await _textToSpeechService.SynthesizeAsync(Text, name);

            WaveformPeaks = await _editorService.GetWaveformPeaksAsync(_pendingClip.FilePath, 300);
            await _playbackService.LoadAsync(_pendingClip);
            _playbackService.Play();

            CanSave = true;
            StatusMessage = "Previewing - tap Save to keep it in your library.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_GenerateFailed(ex);
            StatusMessage = "Couldn't generate that speech clip.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to synthesize speech.")]
    private partial void Log_GenerateFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generating speech ({Length} characters).")]
    private partial void Log_GeneratingSpeech(int length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saving generated speech clip '{ClipName}'.")]
    private partial void Log_SavingClip(string clipName);

    ///<summary>
    ///Persists the pending preview clip into the library (and, best-effort, the shared Music folder).
    ///</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_pendingClip is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Log_SavingClip(_pendingClip.Name);
            AudioClip saved = await _libraryService.AddClipAsync(_pendingClip.FilePath, _pendingClip.Name, isUserRecording: true);
            StatusMessage = saved.PublicStorageLocation is { } location ? $"Saved '{saved.Name}' - also in {location}." : $"Saved '{saved.Name}' to your library.";

            _pendingClip = null;
            CanSave = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Whether there's a generated preview waiting to be saved.
    ///</summary>
    [ObservableProperty]
    public partial bool CanSave { get; set; }

    ///<summary>
    ///The text to synthesize into speech.
    ///</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial string Text { get; set; } = string.Empty;

    ///<summary>
    ///Waveform peaks for the most recently generated preview, used to draw the waveform view.
    ///</summary>
    [ObservableProperty]
    public partial float[] WaveformPeaks { get; set; } = [];
    #endregion
}
