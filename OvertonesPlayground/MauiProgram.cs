using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using OvertonesPlayground.Platforms.Android.Services;
using OvertonesPlayground.Services.Implementations;
using OvertonesPlayground.Services.Interfaces;
using OvertonesPlayground.ViewModels;
using OvertonesPlayground.Views;
using Plugin.Maui.Audio;

namespace OvertonesPlayground;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.AddAudio();

        RegisterServices(builder.Services);
        RegisterViewModelsAndPages(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IPermissionsService, PermissionsService>();
        services.AddSingleton<IAudioLibraryService, AudioLibraryService>();
        services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<IAudioRecorderService, AudioRecorderService>();
        services.AddSingleton<IAudioEditorService, AudioEditorService>();
        services.AddSingleton<IAudioFocusService, AudioFocusService>();
        services.AddSingleton<ISoundSynthesisService, SoundSynthesisService>();
        services.AddSingleton<IPublicStorageService, PublicStorageService>();
    }

    private static void RegisterViewModelsAndPages(IServiceCollection services)
    {
        // Reflect the app's live audio session, so their state survives flyout navigation.
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<PlayerPage>();

        services.AddSingleton<LaunchpadViewModel>();
        services.AddSingleton<LaunchpadPage>();

        services.AddSingleton<MixerViewModel>();
        services.AddSingleton<MixerPage>();

        // Re-created fresh each time they're navigated to.
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<LibraryPage>();

        services.AddTransient<SoundCreatorViewModel>();
        services.AddTransient<SoundCreatorPage>();

        services.AddTransient<ToneGeneratorViewModel>();
        services.AddTransient<ToneGeneratorPage>();

        services.AddTransient<DrumSynthViewModel>();
        services.AddTransient<DrumSynthPage>();

        services.AddTransient<AudioEditorViewModel>();
        services.AddTransient<AudioEditorPage>();

        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsPage>();
    }
}
