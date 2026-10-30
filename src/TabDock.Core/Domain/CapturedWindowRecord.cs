namespace TabDock.Core.Domain;

/// <summary>Una ventana capturada por el grupo <paramref name="GroupId"/>, con lo necesario para restaurarla.</summary>
public sealed record CapturedWindowRecord(Guid GroupId, WindowSnapshot Snapshot);
