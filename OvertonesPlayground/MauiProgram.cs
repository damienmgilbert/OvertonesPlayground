using CommunityToolkit.Maui;
using Microsoft.Maui.DevFlow.Agent;
using OvertonesPlayground.Controls;
using OvertonesPlayground.Platforms.Android.Services;
using OvertonesPlayground.Services.Implementations;
using OvertonesPlayground.Services.Interfaces;
using OvertonesPlayground.Themes;
using OvertonesPlayground.ViewModels;
using OvertonesPlayground.Views;
using Plugin.Maui.Audio;

namespace OvertonesPlayground;

///<summary>
///Configures and builds the MAUI app: fonts, audio plugin, dependency injection, and logging.
///</summary>
public static class MauiProgram
{
    #region Private methods
    ///<summary>
    ///Registers every app service, all as singletons so state (playback, library) is shared across the app.
    ///</summary>
    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IPermissionsService, PermissionsService>();
        services.AddSingleton<IAudioLibraryService, AudioLibraryService>();
        services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<IAudioRecorderService, AudioRecorderService>();
        services.AddSingleton<IAudioEditorService, AudioEditorService>();
        services.AddSingleton<IAudioFocusService, AudioFocusService>();
        services.AddSingleton<IAudioFormatConverterService, AudioFormatConverterService>();
        services.AddSingleton<IMixdownService, MixdownService>();
        services.AddSingleton<ISoundSynthesisService, SoundSynthesisService>();
        services.AddSingleton<IPublicStorageService, PublicStorageService>();
        services.AddSingleton<ITextToSpeechService, TextToSpeechService>();
        services.AddSingleton<ISpeechToTextService, SpeechToTextService>();
    }

    ///<summary>
    ///Registers every page and view model: singletons for the live-session screens (Player/Launchpad/Mixer), transients
    ///elsewhere.
    ///</summary>
    private static void RegisterViewModelsAndPages(IServiceCollection services)
    {
        // Reflect the app's live audio session, so their state survives flyout navigation.
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<PlayerPage>();

        services.AddSingleton<LaunchpadViewModel>();
        services.AddSingleton<LaunchpadPage>();

        services.AddSingleton<MixerViewModel>();
        services.AddSingleton<MixerPage>();

        services.AddSingleton<MultiTrackViewModel>();
        services.AddSingleton<MultiTrackPage>();

        // Speech-to-Text keeps listening/transcript state across flyout navigation (e.g. flipping to another tab
        // mid-listen), and a Transient view model would leak a subscriber on the singleton service's events every
        // time the page is revisited, so it lives in this "live session" bucket too.
        services.AddSingleton<SpeechToTextViewModel>();
        services.AddSingleton<SpeechToTextPage>();

        // Re-created fresh each time they're navigated to.
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<LibraryPage>();

        services.AddTransient<SoundCreatorViewModel>();
        services.AddTransient<SoundCreatorPage>();

        services.AddTransient<ToneGeneratorViewModel>();
        services.AddTransient<ToneGeneratorPage>();

        services.AddTransient<DrumSynthViewModel>();
        services.AddTransient<DrumSynthPage>();

        services.AddTransient<TextToSpeechViewModel>();
        services.AddTransient<TextToSpeechPage>();

        services.AddTransient<AudioEditorViewModel>();
        services.AddTransient<AudioEditorPage>();

        services.AddTransient<TrimViewModel>();
        services.AddTransient<TrimPage>();

        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsPage>();

        // Resolved once in App.CreateWindow so it can receive a logger, rather than being constructed with `new`.
        services.AddTransient<AppShell>();
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Builds the fully-configured <see cref="MauiApp"/> used as the app's host.
    ///</summary>
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(
        fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

            // The icon font: only the glyphs the app uses, cut down from the full Material Symbols font by
            // tools/IconFont/subset-icons.py. Re-run that script after using another IconFont icon, or it draws as an empty box.
            fonts.AddFont("MaterialSymbolsRounded.ttf", Icon.FontFamily);
        });

        builder.AddAudio();
        FluentMotion.Register();

#if ANDROID
        FluentTextInputs.Register();
        AutoTooltips.Register();
#endif

        RegisterServices(builder.Services);
        RegisterViewModelsAndPages(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.AddMauiDevFlowAgent(options =>
        {
            options.EnableLayoutDiagnostics = true;
        });
#endif

        return builder.Build();
    }
    #endregion
}
