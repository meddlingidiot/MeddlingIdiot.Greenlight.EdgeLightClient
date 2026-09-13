using Greenlight.EdgeLightClient;

namespace Greenlight.EdgeLightClient.UnitTests;

/// <summary>
/// Where the strip and the light go, for each of the four edges. At two pixels, a strip one
/// pixel off the edge is a strip that is not there.
/// </summary>
public class EdgeLayoutTests
{
    private static readonly PixelBox Screen = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(ScreenEdge.Top, 0, 0, 1920, 2)]
    [InlineData(ScreenEdge.Bottom, 0, 1078, 1920, 2)]
    [InlineData(ScreenEdge.Left, 0, 0, 2, 1080)]
    [InlineData(ScreenEdge.Right, 1918, 0, 2, 1080)]
    public void TheStripHugsItsEdge(ScreenEdge edge, int x, int y, int w, int h)
    {
        Assert.Equal(new PixelBox(x, y, w, h), EdgeLayout.Strip(Screen, edge, 2));
    }

    [Fact]
    public void AThicknessOfNothingIsStillOnePixel()
    {
        Assert.Equal(1, EdgeLayout.Strip(Screen, ScreenEdge.Top, 0).Height);
    }

    [Theory]
    [InlineData(ScreenEdge.Top, 924, 2)]
    [InlineData(ScreenEdge.Bottom, 924, 1006)]
    [InlineData(ScreenEdge.Left, 2, 504)]
    [InlineData(ScreenEdge.Right, 1846, 504)]
    public void TheLightSitsJustInsideTheStripHalfwayAlong(ScreenEdge edge, int x, int y)
    {
        var box = EdgeLayout.Beacon(Screen, edge, 2, 72, 0.5);

        Assert.Equal(new PixelBox(x, y, 72, 72), box);
    }

    [Fact]
    public void TheEntryPointRunsFromOneEndOfTheEdgeToTheOther()
    {
        Assert.Equal(0, EdgeLayout.Beacon(Screen, ScreenEdge.Top, 2, 72, 0).X);
        Assert.Equal(1920 - 72, EdgeLayout.Beacon(Screen, ScreenEdge.Top, 2, 72, 1).X);
        Assert.Equal(0, EdgeLayout.Beacon(Screen, ScreenEdge.Left, 2, 72, 0).Y);
        Assert.Equal(1080 - 72, EdgeLayout.Beacon(Screen, ScreenEdge.Left, 2, 72, 1).Y);
    }

    [Fact]
    public void AnEntryPointOffTheEndIsKeptOnScreen()
    {
        Assert.Equal(1920 - 72, EdgeLayout.Beacon(Screen, ScreenEdge.Top, 2, 72, 7).X);
        Assert.Equal(0, EdgeLayout.Beacon(Screen, ScreenEdge.Top, 2, 72, -3).X);
    }

    [Fact]
    public void ALightBiggerThanTheScreenStartsAtTheStart()
    {
        Assert.Equal(0, EdgeLayout.Beacon(Screen, ScreenEdge.Top, 2, 4000, 0.5).X);
    }
}
