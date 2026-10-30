using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using TabDock.App.Services;
using TabDock.App.Theming;
using TabDock.App.ViewModels;
using TabDock.App.Views;
using TabDock.Core.Abstractions;
using TabDock.Core.Services;
using TabDock.Infrastructure;
using TabDock.Infrastructure.Diagnostics;
using TabDock.Infrastructure.Persistence;
using TabDock.Win32;
using TabDock.Win32.Shell;

namespace TabDock.App.Hosting;

internal static class AppHost
{
    public static IHost Build(AppPaths paths)
    {
        // Sin valores por defecto: TabDock no lee appsettings.json ni variables de entorno, ni registra en consola.
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSerilog(Log.Logger, dispose: false);
        builder.Services.AddSingleton(paths);
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddOptions<EligibilityOptions>()
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton(sp => new EligibilityPolicy(
            sp.GetRequiredService<IOptions<EligibilityOptions>>().Value,
            Environment.ProcessId));

        builder.Services.AddTabDockWin32();
        builder.Services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton(sp => new GroupingService(
            sp.GetRequiredService<IWindowSystem>(),
            sp.GetRequiredService<EligibilityPolicy>(),
            () => new RuleMatcher(sp.GetRequiredService<SettingsService>().Current.Rules)));
        builder.Services.AddSingleton<IStartupRegistration>(_ => new StartupRegistration(Environment.ProcessPath ?? "TabDock.exe"));
        builder.Services.AddSingleton<DiagnosticsExporter>();
        builder.Services.AddSingleton<DiagnosticsExportService>();
        builder.Services.AddSingleton<RunningProgramsProvider>();
        builder.Services.AddSingleton<IRuntimeStateStore, JsonRuntimeStateStore>();
        builder.Services.AddSingleton<IPreferencesStore, JsonPreferencesStore>();
        builder.Services.AddSingleton(sp =>
        {
            // La purga de preferencias antiguas (90 días) se hace al cargar, en el arranque.
            var service = new TabPreferenceService(sp.GetRequiredService<IPreferencesStore>(), sp.GetRequiredService<TimeProvider>());
            service.Load();
            return service;
        });
        builder.Services.AddSingleton(sp => new RuntimeStateTracker(sp.GetRequiredService<IRuntimeStateStore>(), Environment.ProcessId));
        builder.Services.AddSingleton<SnapshotRestorer>();
        builder.Services.AddSingleton<FrameCoordinator>();
        builder.Services.AddSingleton<HotkeyService>();
        builder.Services.AddSingleton<IHotkeyControl>(sp => sp.GetRequiredService<HotkeyService>());
        builder.Services.AddSingleton<TrayService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<SettingsApplier>();
        builder.Services.AddSingleton<WindowLauncher>();

        AddWindows(builder.Services);

        return builder.Build();
    }

    /// <summary>Ventanas y sus ViewModels: transitorios, se crean al abrirlas.</summary>
    private static void AddWindows(IServiceCollection services)
    {
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<DiagnosticsWindow>();
        services.AddTransient(sp => new SettingsViewModel(
            sp.GetRequiredService<SettingsService>(),
            sp.GetRequiredService<IStartupRegistration>(),
            sp.GetRequiredService<IHotkeyControl>(),
            sp.GetRequiredService<RunningProgramsProvider>().List,
            sp.GetRequiredService<DiagnosticsExportService>().Export));
        services.AddTransient<SettingsWindow>();
        services.AddTransient(sp => new OnboardingViewModel(
            sp.GetRequiredService<SettingsService>(),
            sp.GetRequiredService<RunningProgramsProvider>().List));
        services.AddTransient<OnboardingWindow>();
        services.AddTransient<HostFrameViewModel>();
        services.AddTransient<HostFrameWindow>();
        services.AddSingleton<DemoFrameFactory>();
    }
}
