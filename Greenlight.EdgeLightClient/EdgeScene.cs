namespace Greenlight.EdgeLightClient;

/// <summary>Everything the two canvases need to draw one frame.</summary>
/// <param name="Shown">Which colour the strip is wearing right now. Lags <see cref="EdgeScene.State"/> by a changeover.</param>
/// <param name="StripAlpha">How strongly to draw the strip, 0–1. Folds together the changeover and the pulse.</param>
/// <param name="BeaconReveal">How far out of the edge the hazard light is, 0 (put away) to 1 (fully out). Already eased.</param>
/// <param name="BeaconTurn">Where the light's reflector is pointing, in turns: 0 to the left, 0.25 straight at you, 0.5 to the right, 0.75 away.</param>
public readonly record struct EdgeFrame(StripState Shown, double StripAlpha, double BeaconReveal, double BeaconTurn);

/// <summary>
/// The strip's behaviour, with no drawing in it: which colour is showing, how deep into a
/// pulse it is, and how far the hazard light has come out of the edge.
/// </summary>
/// <remarks>
/// Deliberately free of Avalonia so that it can be tested — a beacon that never quite goes
/// back in after the pipeline is fixed is not something anyone is going to catch on a strip
/// two pixels tall.
/// </remarks>
public sealed class EdgeScene
{
    /// <summary>How long a colour takes to fade out, and the next to fade in.</summary>
    private static readonly TimeSpan Changeover = TimeSpan.FromSeconds(0.3);

    /// <summary>One full pulse — down and back — while a build runs.</summary>
    private static readonly TimeSpan Breath = TimeSpan.FromSeconds(1.6);

    /// <summary>How long the pulse takes to come up when a build starts, and to settle when it ends.</summary>
    private static readonly TimeSpan BreathEase = TimeSpan.FromSeconds(0.6);

    /// <summary>How long the hazard light takes to come out of the edge, and to go back in.</summary>
    private static readonly TimeSpan BeaconSlide = TimeSpan.FromSeconds(0.7);

    /// <summary>One revolution of the reflector. An old rotating beacon is not quick.</summary>
    private static readonly TimeSpan BeaconRevolution = TimeSpan.FromSeconds(1.4);

    private StripState _target = StripState.Off;

    private double _phase;    // where in the breath, in turns
    private double _breath;   // 0..1, how much of the pulse is applied
    private double _slide;    // 0..1, the beacon's slide before easing

    /// <summary>What Greenlight said. Setting it starts a changeover; <see cref="Shown"/> catches up.</summary>
    public StripState State
    {
        get => _target;
        set
        {
            if (_target == value) return;
            _target = value;

            // Nothing on screen to put away, so the first colour after a cold start — or after
            // the strip has faded out — is simply worn rather than waited for.
            if (Fade <= 0) Shown = value;
        }
    }

    /// <summary>Whether a build is running. The pulse comes up while it is and settles when it is not.</summary>
    public bool IsBuilding { get; set; }

    /// <summary>Whether <see cref="StripState.Off"/> is drawn grey, or not drawn at all.</summary>
    public bool ShowWhenOff { get; set; } = true;

    /// <summary>Whether a broken pipeline brings the hazard light out.</summary>
    public bool BeaconEnabled { get; set; } = true;

    /// <summary>How deep the pulse goes, 0–1. See <see cref="EdgeConfig.PulseAmount"/>.</summary>
    public double PulseAmount { get; set; } = 0.6;

    /// <summary>The colour currently on screen.</summary>
    public StripState Shown { get; private set; } = StripState.Off;

    /// <summary>How much of <see cref="Shown"/> is on screen, 0–1. Below 1 during a changeover, or when Off is not drawn.</summary>
    public double Fade { get; private set; }

    /// <summary>Whether a colour is on its way out so the next can come in.</summary>
    public bool IsChangingOver => Shown != _target;

    /// <summary>Where in the pulse the strip is, 0 at full and 1 at its dimmest. Zero for as long as no build is running.</summary>
    public double Pulse { get; private set; }

    /// <summary>How far out the hazard light is, eased, 0 to 1.</summary>
    public double BeaconReveal { get; private set; }

    /// <summary>Where the reflector is pointing, in turns.</summary>
    public double BeaconTurn { get; private set; }

    /// <summary>Whether the hazard light is out at all — on its way out, out, or on its way back in.</summary>
    public bool IsBeaconOut => _slide > 0;

    /// <summary>Advance by one frame's worth of time.</summary>
    public void Advance(TimeSpan elapsed)
    {
        var dt = elapsed.TotalSeconds;
        if (dt <= 0) return;

        AdvanceChangeover(dt);
        AdvanceBreath(dt);
        AdvanceBeacon(dt);
    }

    /// <summary>What to draw right now.</summary>
    public EdgeFrame Frame() =>
        new(Shown, Fade * (1 - PulseAmount * Pulse), BeaconReveal, BeaconTurn);

    private void AdvanceChangeover(double dt)
    {
        var step = dt / Changeover.TotalSeconds;

        if (IsChangingOver)
        {
            // Out first, all the way, and only then the new colour: a strip that blended from
            // green to red where it stood would spend a few frames being a colour that means
            // nothing.
            Fade = Math.Max(0, Fade - step);
            if (Fade <= 0) Shown = _target;
            return;
        }

        var wanted = Shown == StripState.Off && !ShowWhenOff ? 0 : 1;
        Fade = Fade < wanted ? Math.Min(wanted, Fade + step) : Math.Max(wanted, Fade - step);
    }

    private void AdvanceBreath(double dt)
    {
        var ease = dt / BreathEase.TotalSeconds;
        _breath = IsBuilding ? Math.Min(1, _breath + ease) : Math.Max(0, _breath - ease);

        if (_breath <= 0)
        {
            // Parked at full rather than wherever it happened to be, so the next build starts
            // from a solid strip and dims — not from the middle of a fade-up.
            _phase = 0;
            Pulse = 0;
            return;
        }

        _phase = (_phase + dt / Breath.TotalSeconds) % 1;

        // A cosine rather than a triangle: it lingers at the ends and hurries through the
        // middle, which is what breathing looks like and what a sawtooth does not.
        var wave = 0.5 - 0.5 * Math.Cos(_phase * 2 * Math.PI);
        Pulse = _breath * wave;
    }

    private void AdvanceBeacon(double dt)
    {
        // Out on the colour that is actually showing, not the one that is coming: the light
        // emerges as the strip turns red, not a changeover ahead of it.
        var wanted = BeaconEnabled && Shown == StripState.Red && Fade > 0;
        var step = dt / BeaconSlide.TotalSeconds;
        _slide = wanted ? Math.Min(1, _slide + step) : Math.Max(0, _slide - step);

        // Smoothstep: it starts gently, hurries, and settles, rather than arriving at speed and
        // stopping dead against the strip.
        BeaconReveal = _slide * _slide * (3 - 2 * _slide);

        if (_slide <= 0)
        {
            // Parked pointing left, so the next time it comes out it starts its sweep from the
            // same place — a light that appeared mid-flash would read as already having been on.
            BeaconTurn = 0;
            return;
        }

        BeaconTurn = (BeaconTurn + dt / BeaconRevolution.TotalSeconds) % 1;
    }
}
