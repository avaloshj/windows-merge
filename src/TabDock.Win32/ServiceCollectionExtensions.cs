using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TabDock.Core.Abstractions;
using TabDock.Win32.Dwm;

namespace TabDock.Win32;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTabDockWin32(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IWindowSystem, WindowSystem>();
        services.AddSingleton<IWindowEvents, WinEventListener>();
        services.AddSingleton<DwmWindowStyler>();
        services.AddSingleton<IVirtualDesktops, VirtualDesktops>();
        return services;
    }
}
