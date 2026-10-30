namespace TabDock.Core.Domain;

/// <summary>Programa cuyas ventanas se agrupan en un marco propio.</summary>
public sealed record AppRule
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Nombre del ejecutable, sin distinguir mayúsculas (p. ej. <c>Code.exe</c>).</summary>
    public required string ExecutableName { get; init; }

    /// <summary>Patrón opcional de ruta con comodines <c>*</c> y <c>?</c> (p. ej. <c>*\Programs\Microsoft VS Code\*</c>).</summary>
    public string? ExecutablePathPattern { get; init; }

    /// <summary>Clase de ventana opcional (p. ej. <c>Chrome_WidgetWin_1</c>).</summary>
    public string? WindowClassName { get; init; }

    public TitleParserKind TitleParser { get; init; } = TitleParserKind.Generic;

    public bool Enabled { get; init; } = true;

    /// <summary>Las ventanas nuevas de este programa se añaden al grupo existente.</summary>
    public bool AutoMerge { get; init; } = true;

    /// <summary>Al iniciar TabDock se agrupan las ventanas ya abiertas.</summary>
    public bool MergeOnStartup { get; init; }

    public SidebarPosition SidebarPosition { get; init; } = SidebarPosition.Left;

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
