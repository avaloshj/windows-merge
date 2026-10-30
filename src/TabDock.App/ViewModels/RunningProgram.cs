namespace TabDock.App.ViewModels;

/// <summary>Programa con ventanas elegibles abiertas ahora, que se puede añadir como regla.</summary>
/// <param name="DisplayName">Nombre para mostrar (descripción del ejecutable o, si no tiene, su nombre).</param>
/// <param name="ExecutableName">Nombre del ejecutable, p. ej. <c>notepad.exe</c>.</param>
public sealed record RunningProgram(string DisplayName, string ExecutableName)
{
    public override string ToString() => string.Equals(DisplayName, ExecutableName, StringComparison.OrdinalIgnoreCase) ? DisplayName : $"{DisplayName} ({ExecutableName})";
}
