using TabDock.Core.Abstractions;
using TabDock.Core.Services;

namespace TabDock.App.ViewModels;

/// <summary>Fila de la tabla de diagnóstico, ya formateada y localizada.</summary>
public sealed record WindowRowViewModel(
    WindowHandle RawHandle,
    EligibilityStatus StatusKind,
    string Status,
    string Reason,
    string Executable,
    string ClassName,
    int ProcessId,
    string Size,
    string Title,
    string Handle);
