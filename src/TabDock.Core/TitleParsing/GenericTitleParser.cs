namespace TabDock.Core.TitleParsing;

/// <summary>
/// Para cualquier aplicación: si el título tiene la forma <c>documento - Aplicación</c>, el nombre es lo anterior al
/// último separador; si no, el título completo.
/// </summary>
public sealed class GenericTitleParser : ITitleParser
{
    public const string Separator = " - ";

    public ParsedTitle Parse(string? rawTitle)
    {
        var title = rawTitle?.Trim() ?? string.Empty;
        var last = title.LastIndexOf(Separator, StringComparison.Ordinal);
        var name = last > 0 ? title[..last].Trim() : title;
        return new ParsedTitle { ProjectName = name };
    }
}
