namespace TabDock.Core.Services;

/// <summary>Resultado de restaurar las ventanas pendientes de <c>state.json</c>.</summary>
/// <param name="Pending">Ventanas registradas.</param>
/// <param name="Restored">Restauradas correctamente.</param>
/// <param name="Skipped">Ya no existían o su identidad no coincidía (HWND reutilizado): no se tocan.</param>
/// <param name="Failed">Existían pero la restauración falló.</param>
public readonly record struct RecoveryReport(int Pending, int Restored, int Skipped, int Failed);
