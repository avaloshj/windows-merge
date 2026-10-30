namespace TabDock.Core.Domain;

/// <summary>Entorno remoto de una ventana (p. ej. VS Code conectado por SSH, WSL o Dev Container).</summary>
public enum RemoteKind
{
    None,
    Ssh,
    Wsl,
    DevContainer,
    Other,
}
