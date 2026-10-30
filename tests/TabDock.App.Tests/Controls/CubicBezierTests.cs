using TabDock.App.Controls.Motion;

namespace TabDock.App.Tests.Controls;

public sealed class CubicBezierTests
{
    private const double Tolerance = 1e-4;

    [Theory]
    [InlineData(0.22, 1, 0.36, 1)]
    [InlineData(0.4, 0, 1, 1)]
    [InlineData(0.42, 0, 0.58, 1)]
    public void Curve_starts_at_zero_and_ends_at_one(double x1, double y1, double x2, double y2)
    {
        CubicBezier.Evaluate(x1, y1, x2, y2, 0).ShouldBe(0);
        CubicBezier.Evaluate(x1, y1, x2, y2, 1).ShouldBe(1);
        CubicBezier.Evaluate(x1, y1, x2, y2, -0.5).ShouldBe(0);
        CubicBezier.Evaluate(x1, y1, x2, y2, 1.5).ShouldBe(1);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void Linear_control_points_produce_identity(double x)
    {
        CubicBezier.Evaluate(0, 0, 1, 1, x).ShouldBe(x, Tolerance);
    }

    [Fact]
    public void Symmetric_ease_in_out_passes_through_the_midpoint()
    {
        CubicBezier.Evaluate(0.42, 0, 0.58, 1, 0.5).ShouldBe(0.5, Tolerance);
    }

    [Theory]
    [InlineData(0.22, 1, 0.36, 1)]
    [InlineData(0.4, 0, 1, 1)]
    public void Curves_used_by_the_motion_tokens_are_monotonic_without_overshoot(double x1, double y1, double x2, double y2)
    {
        var previous = 0d;
        for (var i = 1; i <= 200; i++)
        {
            var value = CubicBezier.Evaluate(x1, y1, x2, y2, i / 200d);
            value.ShouldBeGreaterThanOrEqualTo(previous - Tolerance);
            value.ShouldBeLessThanOrEqualTo(1 + Tolerance);
            previous = value;
        }
    }

    [Fact]
    public void Standard_ease_is_front_loaded_and_exit_ease_is_back_loaded()
    {
        // Entrada (0.22, 1, 0.36, 1): a mitad de tiempo ya casi ha llegado. Salida (0.4, 0, 1, 1): va lenta al principio.
        CubicBezier.Evaluate(0.22, 1, 0.36, 1, 0.5).ShouldBeGreaterThan(0.85);
        CubicBezier.Evaluate(0.4, 0, 1, 1, 0.5).ShouldBeLessThan(0.4);
    }

    [Fact]
    public void Result_matches_a_brute_force_reference()
    {
        for (var i = 1; i < 20; i++)
        {
            var x = i / 20d;
            CubicBezier.Evaluate(0.22, 1, 0.36, 1, x).ShouldBe(Reference(0.22, 1, 0.36, 1, x), Tolerance);
        }
    }

    /// <summary>Referencia independiente: muestreo denso del parámetro t y búsqueda del x más cercano.</summary>
    private static double Reference(double x1, double y1, double x2, double y2, double x)
    {
        static double Sample(double p1, double p2, double t) => (3 * (1 - t) * (1 - t) * t * p1) + (3 * (1 - t) * t * t * p2) + (t * t * t);

        var bestT = 0d;
        var bestError = double.MaxValue;
        for (var i = 0; i <= 200_000; i++)
        {
            var t = i / 200_000d;
            var error = Math.Abs(Sample(x1, x2, t) - x);
            if (error < bestError)
            {
                bestError = error;
                bestT = t;
            }
        }

        return Sample(y1, y2, bestT);
    }
}
