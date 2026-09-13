using Greenlight.EdgeLightClient;

namespace Greenlight.EdgeLightClient.UnitTests;

/// <summary>
/// The scene, without a window. Everything here is arithmetic somebody would otherwise have to
/// check by watching two pixels along the top of their screen — and the one thing that would
/// make the toy look broken, a hazard light that never quite goes back in, is a thing you
/// would only notice the next morning.
/// </summary>
public class EdgeSceneTests
{
    private const double Step = 1.0 / 60;

    private static EdgeScene Lit(StripState state = StripState.Green)
    {
        var scene = new EdgeScene { State = state };
        Run(scene, 1);
        return scene;
    }

    private static void Run(EdgeScene scene, double seconds)
    {
        for (var t = 0.0; t < seconds; t += Step) scene.Advance(TimeSpan.FromSeconds(Step));
    }

    // ── colours and the changeover ────────────────────────────────────────────

    [Fact]
    public void FirstColourArrivesWithoutAChangeover()
    {
        var scene = new EdgeScene();
        scene.State = StripState.Green;

        Assert.Equal(StripState.Green, scene.Shown);
        Assert.False(scene.IsChangingOver);
    }

    [Fact]
    public void AChangeOfColourFadesTheOldOneOutFirst()
    {
        var scene = Lit();
        scene.State = StripState.Red;

        scene.Advance(TimeSpan.FromSeconds(Step));
        Assert.Equal(StripState.Green, scene.Shown);
        Assert.True(scene.IsChangingOver);

        Run(scene, 1);
        Assert.Equal(StripState.Red, scene.Shown);
        Assert.False(scene.IsChangingOver);
        Assert.Equal(1, scene.Fade);
    }

    [Fact]
    public void GreenlightGoingAwayShowsGreyByDefault()
    {
        var scene = Lit();
        scene.State = StripState.Off;
        Run(scene, 1);

        Assert.Equal(StripState.Off, scene.Shown);
        Assert.Equal(1, scene.Fade);
    }

    [Fact]
    public void GreenlightGoingAwayFadesAllTheWayOutWhenAsked()
    {
        var scene = Lit();
        scene.ShowWhenOff = false;
        scene.State = StripState.Off;
        Run(scene, 1);

        Assert.Equal(0, scene.Fade);
        Assert.Equal(0, scene.Frame().StripAlpha);
    }

    // ── the pulse ─────────────────────────────────────────────────────────────

    [Fact]
    public void SteadyWhenNothingIsBuilding()
    {
        var scene = Lit();
        Run(scene, 3);

        Assert.Equal(0, scene.Pulse);
        Assert.Equal(1, scene.Frame().StripAlpha);
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(0.3, 0.7)]
    [InlineData(0.6, 0.4)]
    [InlineData(1.0, 0.0)]
    public void ThePulseGoesAsDeepAsTheAmountSays(double amount, double expectedFloor)
    {
        var scene = Lit();
        scene.PulseAmount = amount;
        scene.IsBuilding = true;
        Run(scene, 1);

        var dimmest = double.MaxValue;
        var brightest = 0.0;
        for (var t = 0.0; t < 1.6; t += Step)
        {
            scene.Advance(TimeSpan.FromSeconds(Step));
            var alpha = scene.Frame().StripAlpha;
            dimmest = Math.Min(dimmest, alpha);
            brightest = Math.Max(brightest, alpha);
        }

        Assert.Equal(expectedFloor, dimmest, 2);
        Assert.Equal(1.0, brightest, 2);
    }

    [Fact]
    public void ABuildEndingSettlesBackToSteady()
    {
        var scene = Lit();
        scene.IsBuilding = true;
        Run(scene, 2);

        scene.IsBuilding = false;
        Run(scene, 2);

        Assert.Equal(0, scene.Pulse);
        Assert.Equal(1, scene.Frame().StripAlpha);
    }

    // ── the hazard light ──────────────────────────────────────────────────────

    [Fact]
    public void NothingComesOutUnlessThePipelineIsBroken()
    {
        var scene = Lit();
        scene.IsBuilding = true;
        Run(scene, 3);

        Assert.False(scene.IsBeaconOut);
        Assert.Equal(0, scene.Frame().BeaconReveal);

        scene.State = StripState.Amber;
        Run(scene, 3);
        Assert.False(scene.IsBeaconOut);
    }

    [Fact]
    public void RedBringsTheLightOutOnceTheStripIsActuallyRed()
    {
        var scene = Lit();
        scene.State = StripState.Red;

        // Still fading green out: the light waits for the colour it belongs to.
        scene.Advance(TimeSpan.FromSeconds(Step));
        Assert.False(scene.IsBeaconOut);

        Run(scene, 2);
        Assert.True(scene.IsBeaconOut);
        Assert.Equal(1, scene.Frame().BeaconReveal);
    }

    [Fact]
    public void TheLightSlidesRatherThanAppearing()
    {
        var scene = Lit(StripState.Red);

        // Lit() ran a second, which is longer than the slide, so it is fully out. Start again
        // and watch the first frames.
        var fresh = new EdgeScene { State = StripState.Red };
        fresh.Advance(TimeSpan.FromSeconds(Step));
        var first = fresh.Frame().BeaconReveal;
        fresh.Advance(TimeSpan.FromSeconds(Step));
        var second = fresh.Frame().BeaconReveal;

        Assert.True(first > 0 && first < 0.1, $"first frame was {first}");
        Assert.True(second > first);
        Assert.Equal(1, scene.Frame().BeaconReveal);
    }

    [Fact]
    public void TheLightGoesBackInWhenThePipelineIsFixed()
    {
        var scene = Lit(StripState.Red);
        scene.State = StripState.Green;

        // On its way in while green fades in — still out, still turning.
        Run(scene, 0.4);
        Assert.True(scene.IsBeaconOut);

        Run(scene, 2);
        Assert.False(scene.IsBeaconOut);
        Assert.Equal(0, scene.Frame().BeaconReveal);
    }

    [Fact]
    public void TheReflectorTurnsWhileTheLightIsOutAndParksWhenItIsNot()
    {
        var scene = Lit(StripState.Red);
        var before = scene.BeaconTurn;
        Run(scene, 0.5);
        Assert.NotEqual(before, scene.BeaconTurn);
        Assert.InRange(scene.BeaconTurn, 0, 1);

        scene.State = StripState.Green;
        Run(scene, 3);
        Assert.Equal(0, scene.BeaconTurn);
    }

    [Fact]
    public void TheLightCanBeTurnedOffAltogether()
    {
        var scene = Lit(StripState.Red);
        Assert.True(scene.IsBeaconOut);

        scene.BeaconEnabled = false;
        Run(scene, 2);

        Assert.False(scene.IsBeaconOut);
        Assert.Equal(StripState.Red, scene.Shown);   // the strip is still red; only the light is gone
    }
}
