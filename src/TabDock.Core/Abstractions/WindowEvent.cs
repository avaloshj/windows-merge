namespace TabDock.Core.Abstractions;

/// <summary>Evento de una ventana de primer nivel (ya filtrado: <c>OBJID_WINDOW</c> y <c>CHILDID_SELF</c>).</summary>
public readonly record struct WindowEvent(WindowEventKind Kind, WindowHandle Handle);
