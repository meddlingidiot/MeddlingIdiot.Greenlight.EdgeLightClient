using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Greenlight.Sdk;
using Greenlight.Sdk.Protocol;

namespace Greenlight.EdgeLightClient;

/// <summary>
/// The whole of the Greenlight integration, which is the point of the sample: attach, translate
/// the colour, and never care whether Greenlight is actually there.
/// </summary>
/// <remarks>
/// The tray icon and the two windows on the edge are ordinary Avalonia and have nothing
/// to do with Greenlight — the integration is still the twenty-odd lines in
/// <see cref="StartWatchingGreenlight"/>.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class App : Application
{
    private GreenlightClient? _greenlight;
    private StripWindow? _window;
    private EdgeTray? _tray;
    private EdgeConfig _config = new();

    /// <summary>
    /// The last thing Greenlight said. Held here rather than only in the scene because the scene
    /// comes and goes — switched off, switched back on, rebuilt after a reload — and a strip that
    /// came back green after a restart would be the toy lying.
    /// </summary>
    private StripState _state = StripState.Off;

    private bool _building;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The strip window is closed and reopened by the tray's off/on, and there is no other
            // window — on the default setting, switching the strip off would quit the whole thing
            // and take the tray icon with it.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _config = EdgeConfig.Load();

            // If Windows is set to start this, make sure it is still pointed at the right
            // executable. An update moves the versioned copy out from under an older
            // registration, and the symptom is the strip silently not coming back one morning —
            // weeks after anybody touched the setting.
            WindowsStartup.Refresh();

            _tray = new EdgeTray(_config)
            {
                IsRunning = () => _window is not null,
                OnSetRunning = running =>
                {
                    if (running) ShowStrip();
                    else HideStrip();
                },
                OnConfigChanged = () => _window?.ApplyConfig(),
                OnReloadConfig = ReloadConfig,
                OnQuit = () => desktop.Shutdown(),
            };

            ShowStrip();
            StartWatchingGreenlight();

            desktop.Exit += async (_, _) =>
            {
                _tray?.Dispose();
                if (_greenlight is not null) await _greenlight.DisposeAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartWatchingGreenlight()
    {
        _greenlight = new GreenlightClient();

        // Both of these arrive on a background thread — the SDK says so, loudly, and this is what
        // it means in practice. Touching the scene from the pipe's thread would be a race against
        // the frame loop reading the same pulse.
        _greenlight.Changed += (_, e) => Apply(Translate(e.Snapshot.Status), e.Snapshot.IsBuilding);
        _greenlight.AvailabilityChanged += (_, e) =>
        {
            // Anything other than Connected means we have nothing to show, and going grey is
            // more honest than leaving a green strip running on stale data.
            if (e.Availability != GreenlightAvailability.Connected) Apply(StripState.Off, building: false);
        };

        // Deliberately not awaited and deliberately not guarded: StartAsync returns as soon as the
        // background loop is running, and an absent Greenlight is not an error. The strip sits grey
        // until one turns up, then takes its colour on its own.
        _ = _greenlight.StartAsync();
    }

    private void ShowStrip()
    {
        if (_window is not null) return;

        _window = new StripWindow(_config, new EdgeScene
        {
            State = _state,
            IsBuilding = _building,
            ShowWhenOff = _config.ShowWhenOff,
            PulseAmount = _config.PulseAmount,
            BeaconEnabled = _config.Beacon.Enabled,
        });

        _window.Show();
        _tray?.ShowState(_state, _building);
    }

    private void HideStrip()
    {
        _window?.Close();
        _window = null;
        _tray?.ShowState(_state, _building);
    }

    /// <summary>Re-read the file, for colours changed by hand while this was running.</summary>
    private void ReloadConfig()
    {
        _config.CopyFrom(EdgeConfig.Load());
        _window?.ApplyConfig();
    }

    private static StripState Translate(GreenlightStatus status) => status switch
    {
        GreenlightStatus.Green => StripState.Green,
        GreenlightStatus.Yellow => StripState.Amber,
        GreenlightStatus.Red => StripState.Red,
        _ => StripState.Off,
    };

    private void Apply(StripState state, bool building) =>
        Dispatcher.UIThread.Post(() =>
        {
            _state = state;
            _building = building;

            if (_window is not null)
            {
                _window.Scene.State = state;

                // A build under way makes it pulse rather than recolour: Greenlight's own rule
                // is that a broken pipeline stays red while it rebuilds, and a strip that went
                // yellow the moment the fix started would be contradicting it.
                _window.Scene.IsBuilding = building;
            }

            _tray?.ShowState(state, building);
        });
}
