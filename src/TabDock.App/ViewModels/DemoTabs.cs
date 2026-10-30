using TabDock.App.Localization;
using TabDock.Core.Domain;

namespace TabDock.App.ViewModels;

/// <summary>Pestañas ficticias para probar el marco antes de que exista el motor de captura (Fase 3).</summary>
internal static class DemoTabs
{
    public static IReadOnlyList<TabViewModel> Create() =>
    [
        Tab("proyecto-alfa", "index.ts", dirty: true),
        Tab("proyecto-beta", "README.md"),
        Tab("demo-api", "server.ts", remote: RemoteKind.Ssh),
        Tab("servicio-ficticio", "main.py", remote: RemoteKind.Wsl),
        Tab("panel-demo", "docker-compose.yml", remote: RemoteKind.DevContainer, dirty: true),
        Tab("libreria-ejemplo", "Cargo.toml", unresponsive: true),
        Tab(TextResources.Format("Tab_EmptyWindow", 1), subtitle: string.Empty),
    ];

    private static TabViewModel Tab(
        string name,
        string subtitle,
        RemoteKind remote = RemoteKind.None,
        bool dirty = false,
        bool unresponsive = false) =>
        new(name, ProjectColorPalette.ColorFor(name))
        {
            Subtitle = subtitle,
            RemoteKind = remote,
            IsDirty = dirty,
            IsUnresponsive = unresponsive,
        };
}
