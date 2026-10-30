using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Events;
using TabDock.App.Hosting;
using TabDock.App.Localization;
using TabDock.App.Services;
using TabDock.App.Theming;
using TabDock.App.Views;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;
using TabDock.Core.Services;
using TabDock.Infrastructure;
using TabDock.Infrastructure.Diagnostics;
using TabDock.Infrastructure.Logging;
using TabDock.Infrastructure.Persistence;

namespace TabDock.App;

/// <summary>
/// Arranque y garantía de restauración. Rutas que devuelven las ventanas a su estado original:
/// cerrar el marco, salir, cierre de sesión de Windows (<c>SessionEnding</c>), excepción no controlada
/// (restauración de emergencia), muerte del proceso (guardián) y, como última red, el siguiente arranque.
/// </summary>
[SuppressMessage("Design", "MA0049:Type name should not match containing namespace", Justification = "Convención de WPF: la clase Application se llama App.")]
public partial class App : Application
{
    private static readonly TimeSpan s_shutdownTimeout = TimeSpan.FromSeconds(5);

    private IHost? _host;
    private SingleInstanceGuard? _singleInstance;
    private FrameCoordinator? _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Avisos del instalador (instalar, desinstalar): se atienden y el proceso termina antes de seguir.
        Installer.Run();

        var options = CommandLineOptions.Parse(e.Args);
        if (options.GuardianOf is { } parent)
        {
            Shutdown(Guardian.Run(parent.ProcessId, parent.StartTicks));
            return;
        }

        if (options.ExportDiagnosticsTo is { } exportPath)
        {
            Shutdown(ExportDiagnostics(exportPath));
            return;
        }

        // Hasta leer settings.json: el idioma pedido o español (p. ej. para el aviso de instancia única).
        ApplyLanguage(options.Language ?? "es");

        _singleInstance = SingleInstanceGuard.TryAcquire();
        if (_singleInstance is null)
        {
            MessageBox.Show(TextResources.Get("App_AlreadyRunning"), TextResources.Get("App_Name"), MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var paths = AppPaths.CreateDefault();
        paths.EnsureCreated();
        Log.Logger = TabDockLogging.CreateLogger(paths, options.Verbose ? LogEventLevel.Debug : LogEventLevel.Information);
        RegisterGlobalExceptionHandlers();

        _host = AppHost.Build(paths);
        _host.Start();
        var services = _host.Services;
        var user = services.GetRequiredService<SettingsService>().Current.User;
        ApplyUserPreferences(options, user, services.GetRequiredService<ThemeService>());

        Log.Information(
            "TabDock {Version} iniciado en Windows {OsVersion}, idioma {Language}",
            Assembly.GetExecutingAssembly().GetName().Version,
            Environment.OSVersion.Version,
            CultureInfo.CurrentUICulture.Name);

        // Antes de mostrar nada: si la sesión anterior terminó con ventanas ocultas, se devuelven a su sitio.
        services.GetRequiredService<SnapshotRestorer>().RestorePending();
        Guardian.Launch(services.GetRequiredService<ILoggerFactory>().CreateLogger("TabDock.Guardian"));

        _coordinator = services.GetRequiredService<FrameCoordinator>();
        _coordinator.Start();
        SessionEnding += (_, _) => _coordinator.DetachAll();

        // TabDock vive en la bandeja; en modo de prueba (--merge-exe) termina al cerrarse el último marco.
        ShutdownMode = options.MergeExecutable is null ? ShutdownMode.OnExplicitShutdown : ShutdownMode.OnLastWindowClose;
        StartResidentServices(services);
        ShowInitialWindow(options, services);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Restaurar antes de destruir el host; el coordinador lo libera el contenedor de DI.
        _coordinator?.DetachAll();

        if (_host is not null)
        {
            _host.StopAsync(s_shutdownTimeout).GetAwaiter().GetResult();
            _host.Dispose();
            Log.Information("TabDock finalizado con código {ExitCode}", e.ApplicationExitCode);
        }

        Log.CloseAndFlush();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// <c>--export-diagnostics</c>: crea el ZIP sin interfaz, sin agrupar nada y sin escribir logs; funciona aunque haya
    /// otra instancia de TabDock en marcha. Código de salida 0 si se exportó.
    /// </summary>
    private static int ExportDiagnostics(string zipPath)
    {
        var paths = AppPaths.CreateDefault();
        var time = TimeProvider.System;
        var settings = new SettingsService(new JsonSettingsStore(paths, time, NullLogger<JsonSettingsStore>.Instance), time);
        var exporter = new DiagnosticsExporter(paths, time, NullLogger<DiagnosticsExporter>.Instance);
        return new DiagnosticsExportService(exporter, settings).Export(System.IO.Path.GetFullPath(zipPath)).IsSuccess ? 0 : 1;
    }

    /// <summary>
    /// Idioma, modo de dibujo y tema (los argumentos de línea de comandos sustituyen a settings.json). Debe ejecutarse
    /// antes de crear ninguna ventana: el modo de dibujo solo afecta a las que se creen después.
    /// </summary>
    private static void ApplyUserPreferences(CommandLineOptions options, UserSettings user, ThemeService theme)
    {
        ApplyLanguage(options.Language ?? user.Language);

        if (user.LowMemoryRendering)
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        var useMica = (options.UseMica ?? user.UseMica) && !user.LowMemoryRendering;
        theme.Initialize(options.Theme ?? user.Theme, useMica, user.Density);
    }

    /// <summary>Idioma de la interfaz: el de settings.json, salvo que se pida otro con <c>--lang</c>.</summary>
    private static void ApplyLanguage(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>Bandeja, atajos globales, aplicación de ajustes en caliente y reglas con "agrupar al iniciar".</summary>
    private void StartResidentServices(IServiceProvider services)
    {
        var settingsService = services.GetRequiredService<SettingsService>();
        SyncStartWithWindows(settingsService, services.GetRequiredService<IStartupRegistration>());
        var settings = settingsService.Current;
        var tray = services.GetRequiredService<TrayService>();
        tray.Show(settings);
        _coordinator!.Notice += (_, e) => tray.Notify(e.Message, warning: true);
        services.GetRequiredService<WindowLauncher>().Notice += (_, e) => tray.Notify(e.Message, warning: true);

        var hotkeys = services.GetRequiredService<HotkeyService>();
        hotkeys.CommandInvoked += (_, e) => _coordinator!.Execute(e.Command);
        var conflicts = hotkeys.Register(settings.Hotkeys);
        if (conflicts.Count > 0)
            tray.Notify(TextResources.Format("Tray_HotkeyConflicts", string.Join(", ", conflicts.Select(HotkeyText.Format))), warning: true);

        services.GetRequiredService<SettingsApplier>().Start();

        foreach (var rule in settings.Rules.Where(r => r.Enabled && r.MergeOnStartup))
            _ = _coordinator!.MergeAll(rule);
    }

    /// <summary>El registro manda: si el usuario quitó TabDock del inicio por otra vía, la casilla lo refleja.</summary>
    private static void SyncStartWithWindows(SettingsService settings, IStartupRegistration startup)
    {
        var enabled = startup.IsEnabled();
        if (settings.Current.User.StartWithWindows != enabled)
            settings.UpdateUser(u => u with { StartWithWindows = enabled });
    }

    private void ShowInitialWindow(CommandLineOptions options, IServiceProvider services)
    {
        if (options.MergeExecutable is { } executable)
        {
            if (_coordinator!.CaptureExecutable(executable).IsSuccess)
                return;

            Log.Warning("No había ventanas elegibles de {Executable} para agrupar", executable);
            options = options with { Diagnostics = true };
        }

        if (options.DemoFrame)
            services.GetRequiredService<DemoFrameFactory>().Create().Show();
        else if (options.Diagnostics)
            services.GetRequiredService<WindowLauncher>().ShowDiagnostics();

        var launcher = services.GetRequiredService<WindowLauncher>();
        if (options.Settings)
            launcher.ShowSettings();

        // Primer arranque: introducción de 3 pasos (sustituye al aviso de la bandeja de la Fase 4). No en el modo de
        // prueba --merge-exe, que termina al cerrar su última ventana.
        var pending = !services.GetRequiredService<SettingsService>().Current.User.OnboardingCompleted;
        if (options.Onboarding || (pending && options.MergeExecutable is null))
            launcher.ShowOnboarding();
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Excepción no controlada en el hilo de UI");
        _coordinator?.EmergencyRestore();
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.ExceptionObject as Exception, "Excepción no controlada (terminando: {IsTerminating})", e.IsTerminating);
        _coordinator?.EmergencyRestore();
        Log.CloseAndFlush();
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Excepción no observada en una tarea");
        e.SetObserved();
    }
}
