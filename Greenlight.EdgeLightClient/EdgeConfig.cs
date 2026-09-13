using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenlight.EdgeLightClient;

/// <summary>What the strip is wearing. One of these per thing Greenlight can say.</summary>
public enum StripState
{
    /// <summary>No Greenlight to ask. Grey, or nothing at all — see <see cref="EdgeConfig.ShowWhenOff"/>.</summary>
    Off,
    Green,
    Amber,
    Red,
}

/// <summary>Which edge of the primary monitor the strip runs along.</summary>
public enum ScreenEdge
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>The hazard light: whether it comes out, how big it is, and where along the edge.</summary>
public sealed class BeaconConfig
{
    /// <summary>Whether a broken pipeline brings the light out at all. Off, and red is only the strip.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How big the light is — the side of the square it lives in, in logical pixels.</summary>
    public double Size { get; set; } = 72;

    /// <summary>
    /// Where along the edge it comes in, as a fraction of the edge's length: 0 is the start
    /// (left, or top), 1 is the end. Its own square is kept on screen whatever the number.
    /// </summary>
    public double Entry { get; set; } = 0.5;
}

/// <summary>
/// The strip, read from a JSON file the user can edit. Written out with the defaults the first
/// time it is missing, so "where do I change the green" has an answer that does not involve
/// rebuilding anything.
/// </summary>
/// <remarks>
/// Kept in AppData rather than beside the executable: the executable lives under <c>bin</c>,
/// which a rebuild is entitled to delete, and losing somebody's colours to a rebuild would be
/// its own small betrayal.
/// </remarks>
public sealed class EdgeConfig
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Greenlight.EdgeLight", "edgelight.json");

    // ── colours ───────────────────────────────────────────────────────────────
    // Any hex Avalonia can parse — "#4BFF86", or "#CC4BFF86" with its own alpha. The alpha is
    // multiplied by Opacity below, so a translucent colour and a low opacity stack.

    /// <summary>No Greenlight to ask: a cold grey, so the strip is visibly saying nothing.</summary>
    public string WhenOff { get; set; } = "#6A7079";

    /// <summary>Everything passing.</summary>
    public string WhenGreen { get; set; } = "#4BFF86";

    /// <summary>A pull request waiting on you.</summary>
    public string WhenAmber { get; set; } = "#FFCE42";

    /// <summary>A broken pipeline. Also the colour of the hazard light's glass.</summary>
    public string WhenRed { get; set; } = "#FF4E3C";

    /// <summary>Which edge of the primary monitor the strip runs along.</summary>
    public ScreenEdge Edge { get; set; } = ScreenEdge.Top;

    /// <summary>
    /// How thick the strip is, in physical pixels — physical rather than logical because at
    /// this size the difference is the whole strip. One to five is the point of the thing;
    /// the file allows a little more for anyone who wants it.
    /// </summary>
    public int Thickness { get; set; } = 2;

    /// <summary>Overall opacity, for when even a two-pixel line is louder than you want.</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// How deep the pulse goes while a build is running, 0–1. At 0 the strip holds steady
    /// and a build changes nothing; at 1 it fades all the way to nothing and back on every
    /// breath. Somewhere in the middle is a strip that is visibly working without flashing.
    /// </summary>
    public double PulseAmount { get; set; } = 0.6;

    /// <summary>The hazard light that comes out of the edge when a pipeline breaks.</summary>
    public BeaconConfig Beacon { get; set; } = new();

    /// <summary>Draw a grey strip when Greenlight is away, rather than nothing at all.</summary>
    /// <remarks>
    /// On by default. With it off, "Greenlight has stopped" and "the strip has stopped" look
    /// identical, and the difference is exactly the thing a status light exists to show.
    /// </remarks>
    public bool ShowWhenOff { get; set; } = true;

    /// <summary>The colour for a given state. What the canvases ask, every frame.</summary>
    public string ColourFor(StripState state) => state switch
    {
        StripState.Green => WhenGreen,
        StripState.Amber => WhenAmber,
        StripState.Red => WhenRed,
        _ => WhenOff,
    };

    /// <summary>
    /// Load the file, writing the defaults out first if it is not there. A file that cannot be
    /// read or parsed falls back to the defaults rather than refusing to start: this is a desk
    /// toy, and a stray comma should not cost you the whole thing.
    /// </summary>
    public static EdgeConfig Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            if (!File.Exists(file))
            {
                var fresh = new EdgeConfig();
                fresh.Save(file);
                return fresh;
            }

            var loaded = JsonSerializer.Deserialize<EdgeConfig>(File.ReadAllText(file), Json);
            if (loaded is null) return new EdgeConfig();

            // A file written before one of these existed deserializes it as null, and so does a
            // hand edit that deleted a line. Neither should be a crash on the next frame.
            var defaults = new EdgeConfig();
            loaded.WhenOff ??= defaults.WhenOff;
            loaded.WhenGreen ??= defaults.WhenGreen;
            loaded.WhenAmber ??= defaults.WhenAmber;
            loaded.WhenRed ??= defaults.WhenRed;
            loaded.Beacon ??= defaults.Beacon;

            // Clamped on the way in, not only on the way out. The file is hand-editable, and a
            // thickness of 400 should give you a fat strip rather than a window over half the
            // screen.
            loaded.Thickness = Math.Clamp(loaded.Thickness, 1, 12);
            loaded.Opacity = Math.Clamp(loaded.Opacity, 0.1, 1.0);
            loaded.PulseAmount = Math.Clamp(loaded.PulseAmount, 0.0, 1.0);
            loaded.Beacon.Size = Math.Clamp(loaded.Beacon.Size, 24, 300);
            loaded.Beacon.Entry = Math.Clamp(loaded.Beacon.Entry, 0.0, 1.0);

            return loaded;
        }
        catch
        {
            return new EdgeConfig();
        }
    }

    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // A toy that cannot write its config still runs perfectly well on the defaults.
        }
    }

    /// <summary>Take on everything from a freshly-read file, in place.</summary>
    /// <remarks>
    /// Copied into this instance rather than swapping it for the new one: the tray is holding
    /// this object, and it is the tray's menu that has to keep agreeing with the file.
    /// </remarks>
    public void CopyFrom(EdgeConfig other)
    {
        WhenOff = other.WhenOff;
        WhenGreen = other.WhenGreen;
        WhenAmber = other.WhenAmber;
        WhenRed = other.WhenRed;
        Edge = other.Edge;
        Thickness = other.Thickness;
        Opacity = other.Opacity;
        PulseAmount = other.PulseAmount;
        Beacon = other.Beacon;
        ShowWhenOff = other.ShowWhenOff;
    }
}
