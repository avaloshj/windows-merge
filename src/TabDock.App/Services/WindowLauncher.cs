using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TabDock.App.Localization;
using TabDock.App.Views;

namespace TabDock.App.Services;

/// <summary>
/// Abre Ajustes, la introducción y el diagnóstico de ventanas. Ajustes y la introducción son únicas: si ya están
/// abiertas, se traen al frente. Un fallo al crear una ventana (p. ej. un error de XAML) no debe cerrar TabDock: se
/// registra y se avisa, y los grupos siguen funcionando.
/// </summary>
public sealed partial class WindowLauncher(IServiceProvider services, ILogger<WindowLauncher> logger)
{
    private SettingsWindow? _settings;
    private OnboardingWindow? _onboarding;

    /// <summary>Aviso para el usuario cuando una ventana no se puede abrir (lo muestra la bandeja).</summary>
    public event EventHandler<NoticeEventArgs>? Notice;

    public void ShowSettings() => _settings = ShowSingle(_settings, () => services.GetRequiredService<SettingsWindow>(), () => _settings = null);

    public void ShowOnboarding() => _onboarding = ShowSingle(_onboarding, () => services.GetRequiredService<OnboardingWindow>(), () => _onboarding = null);

    public void ShowDiagnostics() => _ = TryOpen(() => services.GetRequiredService<DiagnosticsWindow>());

    private T? ShowSingle<T>(T? current, Func<T> create, Action closed)
        where T : Window
    {
        if (current is not null)
        {
            if (current.WindowState == WindowState.Minimized)
                current.WindowState = WindowState.Normal;

            current.Activate();
            return current;
        }

        var window = TryOpen(create);
        if (window is not null)
            window.Closed += (_, _) => closed();

        return window;
    }

    private T? TryOpen<T>(Func<T> create)
        where T : Window
    {
        try
        {
            var window = create();
            window.Show();
            window.Activate();
            return window;
        }
        catch (Exception ex) when (ex is System.Windows.Markup.XamlParseException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            LogOpenFailed(ex, typeof(T).Name);
            Notice?.Invoke(this, new NoticeEventArgs(TextResources.Get("Error_WindowOpen")));
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo abrir la ventana {Window}")]
    private partial void LogOpenFailed(Exception exception, string window);
}
