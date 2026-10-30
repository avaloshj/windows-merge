using System.Globalization;
using System.Xml.Linq;

namespace TabDock.App.Tests.Themes;

/// <summary>
/// Tokens de movimiento (Motion.xaml) frente a la especificación de diseño, y su variante para "Reducir animaciones"
/// de Windows (Motion.Reduced.xaml): solo fundidos de 120 ms como máximo, sin desplazamiento, escala ni altura.
/// </summary>
public sealed class MotionTokensTests
{
    private static readonly TimeSpan s_reducedMaximum = TimeSpan.FromMilliseconds(120);

    [Fact]
    public void Both_dictionaries_define_the_same_tokens()
    {
        Load("Motion.Reduced").Keys.Order(StringComparer.Ordinal).ShouldBe(Load("Motion").Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Motion.Duration.Hover", 120)]
    [InlineData("Motion.Duration.Indicator", 200)]
    [InlineData("Motion.Duration.Frame", 250)]
    [InlineData("Motion.Duration.Entrance", 300)]
    [InlineData("Motion.Duration.Collapse", 180)]
    public void Durations_match_the_design_spec(string key, int milliseconds)
    {
        Duration(Load("Motion")[key]).ShouldBe(TimeSpan.FromMilliseconds(milliseconds));
    }

    [Fact]
    public void Movement_never_exceeds_12_px_and_never_overshoots()
    {
        var motion = Load("Motion");

        Math.Abs(Number(motion["Motion.Entrance.OffsetX"])).ShouldBeLessThanOrEqualTo(12);
        Number(motion["Motion.Frame.ScaleFrom"]).ShouldBeInRange(0.95, 1.0);
        Number(motion["Motion.Press.Scale"]).ShouldBeInRange(0.95, 1.0);

        // Sin rebote: los puntos de control de la curva no salen de [0, 1].
        foreach (var ease in new[] { "Motion.Ease.Standard", "Motion.Ease.Exit" })
        {
            foreach (var axis in new[] { "X1", "Y1", "X2", "Y2" })
                Number(motion[ease].Attribute(axis)!.Value).ShouldBeInRange(0, 1);
        }
    }

    [Fact]
    public void Reduced_motion_only_uses_short_fades()
    {
        var reduced = Load("Motion.Reduced");

        foreach (var duration in reduced.Where(t => t.Value.Name.LocalName == "Duration"))
            Duration(duration.Value).ShouldBeLessThanOrEqualTo(s_reducedMaximum, duration.Key);

        Number(reduced["Motion.Entrance.OffsetX"]).ShouldBe(0);
        Number(reduced["Motion.Frame.ScaleFrom"]).ShouldBe(1);
        Number(reduced["Motion.Press.Scale"]).ShouldBe(1);
        Number(reduced["Motion.Stagger.Milliseconds"]).ShouldBe(0);
        reduced["Motion.Collapse.AnimateHeight"].Value.ShouldBe("False");
    }

    private static Dictionary<string, XElement> Load(string name)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Themes", name + ".xaml");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(path).Root!.Elements()
            .Where(e => e.Attribute(x + "Key") is not null)
            .ToDictionary(e => e.Attribute(x + "Key")!.Value, e => e, StringComparer.Ordinal);
    }

    private static TimeSpan Duration(XElement element) => TimeSpan.Parse(element.Value, CultureInfo.InvariantCulture);

    private static double Number(XElement element) => Number(element.Value);

    private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
}
