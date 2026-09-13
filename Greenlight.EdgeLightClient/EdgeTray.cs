using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Greenlight.EdgeLightClient;

/// <summary>
/// The mascot in the notification area, and the menu hanging off him: the only part of this toy
/// a person can click.
/// </summary>
/// <remarks>
/// <para>
/// The strip is click-through by design — it is two pixels along the edge of the screen, and
/// two pixels that swallowed clicks would be two pixels the taskbar, the title bar and the
/// scroll bar were all missing. That leaves the tray for everything: every setting in
/// <see cref="EdgeConfig"/> that can be changed while the thing is running is reachable from
/// here, and each change is written straight back to the file, so the menu and the JSON are
/// always the same settings.
/// </para>
/// <para>
/// Avalonia's own <see cref="TrayIcon"/> rather than a tray library, because the sample is meant
/// to be readable — and because a sample that drags in a dependency to draw one icon is making a
/// point nobody asked for.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class EdgeTray : IDisposable
{
    private static readonly Uri IconUri = new("avares://Greenlight.EdgeLightClient/Assets/MeddlingIdiot.ico");

    private readonly EdgeConfig _config;
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _status;
    private readonly NativeMenuItem _running;
    private readonly NativeMenuItem _startup;

    public EdgeTray(EdgeConfig config)
    {
        _config = config;

        _status = new NativeMenuItem { Header = "Waiting for Greenlight…", IsEnabled = false };

        _running = new NativeMenuItem
        {
            Header = "Strip on the screen",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = true,
        };
        _running.Click += (_, _) => SetRunning(!IsRunning?.Invoke() ?? true);

        // Read from the registry rather than from a setting of ours, every time it is shown: the
        // user can turn this off in Task Manager's Startup tab, and a tick remembering what we
        // last wrote would then be telling them the opposite of the truth.
        _startup = Check("Start with Windows", WindowsStartup.IsEnabled, value => WindowsStartup.Set(value));

        var menu = BuildMenu();

        // The top-level items are not inside a submenu, so nothing else re-ticks them. Only the
        // startup one can actually change behind our back, but it can, and this is the moment to
        // notice.
        menu.Opening += (_, _) => _startup.IsChecked = WindowsStartup.IsEnabled();

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(IconUri)),
            ToolTipText = "Greenlight edge light",
            Menu = menu,
            IsVisible = true,
        };

        // The one thing a left click can mean here. There is no main window to open, and a tray
        // icon that does nothing at all when clicked reads as a hung one.
        _tray.Clicked += (_, _) => SetRunning(!IsRunning?.Invoke() ?? true);
    }

    /// <summary>Whether the strip is currently on the screen.</summary>
    public Func<bool>? IsRunning { get; set; }

    /// <summary>Put the strip on the screen, or take it off.</summary>
    public Action<bool>? OnSetRunning { get; set; }

    /// <summary>
    /// A setting changed that the strip can absorb where it stands — which is all of them,
    /// because it is drawn and laid out from the config each frame.
    /// </summary>
    public Action? OnConfigChanged { get; set; }

    /// <summary>Re-read the file, for colours changed by hand.</summary>
    public Action? OnReloadConfig { get; set; }

    public Action? OnQuit { get; set; }

    /// <summary>Say what the strip is doing, in the tooltip and at the top of the menu.</summary>
    public void ShowState(StripState state, bool building)
    {
        var running = IsRunning?.Invoke() ?? true;

        _status.Header = state switch
        {
            StripState.Green => building ? "Greenlight: green — building" : "Greenlight: green — all passing",
            StripState.Amber => building
                ? "Greenlight: yellow — building"
                : "Greenlight: yellow — a pull request wants you",
            StripState.Red => building ? "Greenlight: red — rebuilding" : "Greenlight: red — a pipeline is broken",
            _ => "Greenlight not running — nothing claimed",
        };

        _running.IsChecked = running;

        _tray.ToolTipText = running
            ? $"Greenlight edge light — {Short(state)}{(building ? ", building" : string.Empty)}"
            : "Greenlight edge light — off";
    }

    private static string Short(StripState state) => state switch
    {
        StripState.Green => "green",
        StripState.Amber => "yellow",
        StripState.Red => "red",
        _ => "not connected",
    };

    public void Dispose()
    {
        _tray.IsVisible = false;
        _tray.Dispose();
    }

    private void SetRunning(bool running)
    {
        OnSetRunning?.Invoke(running);
        _running.IsChecked = running;
        _tray.ToolTipText = running ? "Greenlight edge light" : "Greenlight edge light — off";
    }

    private NativeMenu BuildMenu() =>
    [
        _status,
        new NativeMenuItemSeparator(),
        _running,
        new NativeMenuItemSeparator(),
        Submenu("Which edge",
            Edge("Top", ScreenEdge.Top),
            Edge("Bottom", ScreenEdge.Bottom),
            Edge("Left", ScreenEdge.Left),
            Edge("Right", ScreenEdge.Right)),
        Submenu("How thick",
            Thickness("A hairline", 1),
            Thickness("Two pixels", 2),
            Thickness("Three pixels", 3),
            Thickness("Five pixels", 5)),
        Submenu("How solid",
            Opacity("Solid", 1.0),
            Opacity("Nearly solid", 0.8),
            Opacity("Half there", 0.5),
            Opacity("Barely there", 0.3)),
        Submenu("How much it pulses",
            Pulse("Not at all", 0.0),
            Pulse("Gently", 0.3),
            Pulse("Plenty", 0.6),
            Pulse("All the way out", 1.0)),
        Submenu("The hazard light",
            Check("Comes out when a pipeline breaks",
                () => _config.Beacon.Enabled,
                value =>
                {
                    _config.Beacon.Enabled = value;
                    Persist();
                    OnConfigChanged?.Invoke();
                }),
            new NativeMenuItemSeparator(),
            BeaconSize("Small", 48),
            BeaconSize("Ordinary", 72),
            BeaconSize("Large", 110),
            BeaconSize("Hard to miss", 160),
            new NativeMenuItemSeparator(),
            Entry("Near the start", 0.1),
            Entry("A quarter along", 0.25),
            Entry("In the middle", 0.5),
            Entry("Three quarters along", 0.75),
            Entry("Near the end", 0.9)),
        Check("Grey strip when Greenlight is away",
            () => _config.ShowWhenOff,
            value =>
            {
                _config.ShowWhenOff = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        _startup,
        new NativeMenuItemSeparator(),
        Item("Edit the colours…", EditConfig),
        Item("Reload the file", () => OnReloadConfig?.Invoke()),
        new NativeMenuItemSeparator(),
        Item("Quit", () => OnQuit?.Invoke()),
    ];

    // ── the settings ──────────────────────────────────────────────────────────
    // Deliberately not here: the colours. Four hex strings are not something anybody wants to
    // pick off a menu, so the menu's job there is just to make the file findable.

    private NativeMenuItem Edge(string header, ScreenEdge edge) =>
        Choice(header, () => _config.Edge == edge, () =>
        {
            _config.Edge = edge;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Thickness(string header, int pixels) =>
        Choice(header, () => _config.Thickness == pixels, () =>
        {
            _config.Thickness = pixels;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Opacity(string header, double opacity) =>
        Choice(header, () => Math.Abs(_config.Opacity - opacity) < 0.001, () =>
        {
            _config.Opacity = opacity;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Pulse(string header, double amount) =>
        Choice(header, () => Math.Abs(_config.PulseAmount - amount) < 0.001, () =>
        {
            _config.PulseAmount = amount;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem BeaconSize(string header, double size) =>
        Choice(header, () => Math.Abs(_config.Beacon.Size - size) < 0.001, () =>
        {
            _config.Beacon.Size = size;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Entry(string header, double entry) =>
        Choice(header, () => Math.Abs(_config.Beacon.Entry - entry) < 0.001, () =>
        {
            _config.Beacon.Entry = entry;
            Persist();
            OnConfigChanged?.Invoke();
        });

    // ── Menu plumbing ─────────────────────────────────────────────────────────
    // Each option asks the config what it should look like when the menu opens rather than being
    // ticked once at startup: the file is editable by hand and reloadable from this very menu, so
    // anything remembering its own state would start lying the moment it was.

    private static NativeMenuItem Check(string header, Func<bool> isOn, Action<bool> set)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isOn(),

            // So a submenu's re-tick keeps a check box honest too, not only the radio items.
            CommandParameter = isOn,
        };

        item.Click += (_, _) =>
        {
            set(!isOn());
            item.IsChecked = isOn();
        };

        return item;
    }

    private static NativeMenuItem Item(string header, Action click)
    {
        var item = new NativeMenuItem { Header = header };
        item.Click += (_, _) => click();
        return item;
    }

    private static NativeMenuItem Submenu(string header, params NativeMenuItemBase[] items)
    {
        var menu = new NativeMenu();
        foreach (var item in items) menu.Add(item);

        var choices = items.OfType<NativeMenuItem>().ToArray();

        void Retick()
        {
            foreach (var item in choices)
                if (item.CommandParameter is Func<bool> isChosen)
                    item.IsChecked = isChosen();
        }

        // Twice, because neither moment is reliable on its own: picking an option has to move
        // the tick off the old one straight away, and opening the menu has to account for the
        // file having been edited by hand behind its back.
        foreach (var item in choices) item.Click += (_, _) => Retick();
        menu.Opening += (_, _) => Retick();

        return new NativeMenuItem { Header = header, Menu = menu };
    }

    private static NativeMenuItem Choice(string header, Func<bool> isChosen, Action choose)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = isChosen(),

            // Parked here rather than in a dictionary: the menu owns its items, and a second
            // collection to keep in step with it is a second thing to get wrong.
            CommandParameter = isChosen,
        };

        item.Click += (_, _) => choose();
        return item;
    }

    private void Persist() => _config.Save();

    /// <summary>
    /// Open <c>edgelight.json</c> in whatever the machine opens JSON with. The four colours are
    /// the one thing that is not on the menu, so the menu's job there is to make the file
    /// findable.
    /// </summary>
    private void EditConfig()
    {
        try
        {
            // It is written out on first run, but a deleted file should still open something
            // rather than nothing.
            if (!File.Exists(EdgeConfig.DefaultPath)) _config.Save();

            Process.Start(new ProcessStartInfo(EdgeConfig.DefaultPath) { UseShellExecute = true });
        }
        catch
        {
            // No editor associated with .json, or the shell refused. A desk toy does not get to
            // interrupt anyone over it.
        }
    }
}
