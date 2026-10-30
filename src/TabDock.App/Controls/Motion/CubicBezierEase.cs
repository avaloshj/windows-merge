using System.Windows;
using System.Windows.Media.Animation;

namespace TabDock.App.Controls.Motion;

/// <summary>
/// Función de easing <c>cubic-bezier</c> para animaciones WPF. Implementa <see cref="IEasingFunction"/> directamente
/// (no <see cref="EasingFunctionBase"/>) para que <c>EasingMode</c> no deforme la curva.
/// </summary>
public sealed class CubicBezierEase : Freezable, IEasingFunction
{
    public static readonly DependencyProperty X1Property = DependencyProperty.Register(nameof(X1), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(0d));
    public static readonly DependencyProperty Y1Property = DependencyProperty.Register(nameof(Y1), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(0d));
    public static readonly DependencyProperty X2Property = DependencyProperty.Register(nameof(X2), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(1d));
    public static readonly DependencyProperty Y2Property = DependencyProperty.Register(nameof(Y2), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(1d));

    public double X1
    {
        get => (double)GetValue(X1Property);
        set => SetValue(X1Property, value);
    }

    public double Y1
    {
        get => (double)GetValue(Y1Property);
        set => SetValue(Y1Property, value);
    }

    public double X2
    {
        get => (double)GetValue(X2Property);
        set => SetValue(X2Property, value);
    }

    public double Y2
    {
        get => (double)GetValue(Y2Property);
        set => SetValue(Y2Property, value);
    }

    public double Ease(double normalizedTime) => CubicBezier.Evaluate(X1, Y1, X2, Y2, normalizedTime);

    protected override Freezable CreateInstanceCore() => new CubicBezierEase();
}
