namespace TabDock.Core.TitleParsing;

/// <summary>Plantilla de <c>window.title</c> que usa la ventana.</summary>
public enum VsCodeTitleTemplate
{
    /// <summary>
    /// Plantilla por defecto de VS Code:
    /// <c>${dirty}${activeEditorShort}${separator}${rootName}${separator}${profileName}${separator}${appName}</c>.
    /// </summary>
    Default,

    /// <summary>Recomendada por TabDock (más fiable): <c>${rootName}${separator}${activeEditorShort}${separator}${appName}</c>.</summary>
    RootFirst,
}
