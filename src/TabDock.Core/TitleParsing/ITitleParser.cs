namespace TabDock.Core.TitleParsing;

public interface ITitleParser
{
    ParsedTitle Parse(string? rawTitle);
}
