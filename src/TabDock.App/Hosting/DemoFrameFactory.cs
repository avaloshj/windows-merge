using Microsoft.Extensions.DependencyInjection;
using TabDock.App.Localization;
using TabDock.App.ViewModels;
using TabDock.App.Views;

namespace TabDock.App.Hosting;

/// <summary>Crea un marco anfitrión con pestañas ficticias para revisar el sistema visual sin ventanas reales.</summary>
public sealed class DemoFrameFactory(IServiceProvider services)
{
    public HostFrameWindow Create()
    {
        var window = services.GetRequiredService<HostFrameWindow>();
        ((HostFrameViewModel)window.DataContext).Load(TextResources.Get("Frame_DemoTitle"), DemoTabs.Create());
        return window;
    }
}
