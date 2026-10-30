namespace TabDock.Core.Domain;

public enum TitleParserKind
{
    /// <summary>Título de VS Code y derivados (Insiders, VSCodium, Cursor).</summary>
    VsCode,

    /// <summary>Cualquier aplicación: el último segmento " - " se trata como nombre de la aplicación.</summary>
    Generic,
}
