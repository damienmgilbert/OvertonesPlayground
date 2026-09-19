# OvertonesPlayground

## Testing

```bash
dotnet test --project OvertonesPlayground.Tests
```

The tests run on the desktop host: no device, emulator or MAUI workload run-time is needed, and the whole suite takes a few
seconds.

### What is tested where

The app targets `net10.0-android` only, so a host-run test project can't reference it. Instead
[`OvertonesPlayground.Tests`](OvertonesPlayground.Tests/OvertonesPlayground.Tests.csproj) compiles the app's own source files
straight into the test assembly, and only the ones that can run on a plain .NET host:

| Code | Tested how |
| --- | --- |
| `Models`, `Services/Interfaces`, `ViewModels` | Unit tests, with fakes for everything a view model depends on. |
| Service implementations that reach the device only through an injected interface (`IFileSystem`, `IPreferences`, `IFilePicker`, `IAudioManager`, `IAudioRecorder`...) | Unit tests, with real temp files and fakes for the platform API. The DSP code (filters, FFT, noise reduction, drum synthesis, WAV reading and writing) is plain C# and is tested directly. |
| `Platforms/**`, `Views/**`, `Controls/**`, `Themes/**`, `PermissionsService`, `ShellNavigationService`, every `*.xaml` | **Not** unit-tested: they need handlers, native controls, permissions or a running Shell. Check them on a device, for example with [DevFlow](https://github.com/dotnet/maui-labs) (`maui devflow ui tree`, `ui tap --automationId ...`). |

### Keeping code testable

View models and services must not call the static MAUI platform APIs (`Preferences.Default`, `FileSystem.AppDataDirectory`,
`Shell.Current`, `Clipboard.Default`, `DeviceDisplay.Current`...). They take the interface instead, and `MauiProgram`
registers the real implementation:

| Instead of | Inject |
| --- | --- |
| `Preferences.Default` | `IPreferences` |
| `FileSystem.AppDataDirectory` / `CacheDirectory` | `IFileSystem` |
| `FilePicker.Default` | `IFilePicker` |
| `Clipboard.Default`, `Share.Default` | `IClipboard`, `IShare` |
| `DeviceDisplay.Current`, `AppInfo.Current` | `IDeviceDisplay`, `IAppInfo` |
| `Shell.Current.GoToAsync(route)` | `INavigationService` |
| `Application.Current.UserAppTheme` | `IThemeService` |
| `TeachingTips` (static) | `ITeachingTipsService` |

The tests use `TestSupport/FakePreferences` and `TestSupport/TempFileSystem` for the first two, and NSubstitute for the rest.

### Adding a test for a new file

* A new view model, model or service interface is picked up automatically (they are linked by wildcard).
* A new **service implementation** has to be added to the explicit list in the test project's `.csproj`. That is deliberate, so a
  device-bound service is never linked by accident. If it won't compile there, it still calls a static platform API.
