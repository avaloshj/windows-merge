namespace TabDock.App.Controls.Motion;

/// <summary>
/// Curva de Bézier cúbica con extremos (0,0) y (1,1), equivalente a <c>cubic-bezier(x1, y1, x2, y2)</c> de CSS.
/// </summary>
internal static class CubicBezier
{
    private const double Epsilon = 1e-6;
    private const int NewtonIterations = 8;

    /// <summary>Devuelve el progreso de la animación (eje Y) para un tiempo normalizado <paramref name="x"/> en [0, 1].</summary>
    public static double Evaluate(double x1, double y1, double x2, double y2, double x)
    {
        if (x <= 0)
            return 0;

        if (x >= 1)
            return 1;

        return Sample(y1, y2, SolveForT(x1, x2, x));
    }

    private static double Sample(double p1, double p2, double t)
    {
        var u = 1 - t;
        return (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t);
    }

    private static double Derivative(double p1, double p2, double t)
    {
        var u = 1 - t;
        return (3 * u * u * p1) + (6 * u * t * (p2 - p1)) + (3 * t * t * (1 - p2));
    }

    /// <summary>Busca t tal que X(t) = x: Newton-Raphson y, si no converge, bisección (X es monótona en [0, 1]).</summary>
    private static double SolveForT(double x1, double x2, double x)
    {
        var t = x;
        for (var i = 0; i < NewtonIterations; i++)
        {
            var error = Sample(x1, x2, t) - x;
            if (Math.Abs(error) < Epsilon)
                return t;

            var slope = Derivative(x1, x2, t);
            if (Math.Abs(slope) < Epsilon)
                break;

            t -= error / slope;
            if (t is < 0 or > 1)
                break;
        }

        double low = 0, high = 1;
        t = x;
        while (high - low > Epsilon)
        {
            var value = Sample(x1, x2, t);
            if (Math.Abs(value - x) < Epsilon)
                return t;

            if (value < x)
                low = t;
            else
                high = t;

            t = (low + high) / 2;
        }

        return t;
    }
}
