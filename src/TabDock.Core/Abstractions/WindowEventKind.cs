namespace TabDock.Core.Abstractions;

/// <summary>Eventos de ventana de <c>SetWinEventHook</c> que TabDock escucha.</summary>
public enum WindowEventKind
{
    Created,
    Shown,
    Hidden,
    Destroyed,
    NameChanged,
    LocationChanged,
    Foreground,
    MinimizeStart,
    MinimizeEnd,
    MoveSizeStart,
    MoveSizeEnd,
    Cloaked,
    Uncloaked,
}
