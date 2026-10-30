using TabDock.Core.Domain;

namespace TabDock.Core.TitleParsing;

public static class TitleParserFactory
{
    private static readonly VsCodeTitleParser s_vsCode = new();
    private static readonly GenericTitleParser s_generic = new();

    public static ITitleParser For(TitleParserKind kind) => kind == TitleParserKind.VsCode ? s_vsCode : s_generic;
}
