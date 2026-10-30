using System.Windows;
using System.Windows.Media.Animation;

namespace TabDock.App.Controls.Motion;

/// <summary>Envoltorio de <see cref="DoubleAnimation"/> que devuelve una tarea y deja el valor final como valor base.</summary>
internal static class Animator
{
    /// <summary>
    /// Anima <paramref name="property"/> hasta <paramref name="to"/>. Con duración cero aplica el valor al instante.
    /// Al terminar, el valor final pasa a ser el valor base y se retira la animación (así otra animación puede
    /// partir de él y no queda un valor "retenido" que oculte cambios posteriores).
    /// </summary>
    public static Task RunAsync(
        DependencyObject target,
        DependencyProperty property,
        double? from,
        double to,
        TimeSpan duration,
        IEasingFunction? ease,
        TimeSpan beginTime = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var animatable = (IAnimatable)target;

        if (duration <= TimeSpan.Zero && beginTime <= TimeSpan.Zero)
        {
            animatable.BeginAnimation(property, animation: null);
            target.SetValue(property, to);
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource();
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = duration,
            BeginTime = beginTime,
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd,
        };
        if (from is { } start)
            animation.From = start;

        animation.Completed += (_, _) =>
        {
            target.SetValue(property, to);
            animatable.BeginAnimation(property, animation: null);
            completion.TrySetResult();
        };

        animatable.BeginAnimation(property, animation);
        return completion.Task;
    }
}
