using TabDock.Core.Domain;
using TabDock.Core.TitleParsing;

namespace TabDock.Core.Tests.TitleParsing;

public sealed class GenericTitleParserTests
{
    [Theory]
    [InlineData("Documento-ficticio - Procesador", "Documento-ficticio")]
    [InlineData("informe - borrador - Procesador", "informe - borrador")]
    [InlineData("Calculadora", "Calculadora")]
    [InlineData("  espacios  ", "espacios")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Name_is_everything_before_the_application_name(string? title, string expected)
    {
        new GenericTitleParser().Parse(title).ProjectName.ShouldBe(expected);
    }

    [Fact]
    public void Factory_returns_the_parser_for_each_kind()
    {
        TitleParserFactory.For(TitleParserKind.VsCode).ShouldBeOfType<VsCodeTitleParser>();
        TitleParserFactory.For(TitleParserKind.Generic).ShouldBeOfType<GenericTitleParser>();
    }
}
