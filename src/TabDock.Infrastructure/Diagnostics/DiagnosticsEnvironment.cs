using TabDock.Core.Diagnostics;

namespace TabDock.Infrastructure.Diagnostics;

/// <summary>Datos del equipo que se incluyen en el diagnóstico. No contiene nombres de equipo ni de usuario.</summary>
public sealed record DiagnosticsEnvironment(
    string AppVersion,
    string OsVersion,
    string ProcessArchitecture,
    string UiCulture,
    IReadOnlyList<DisplayInfo> Displays);
