using TabDock.Core.Domain;

namespace TabDock.Core.Tests.Domain;

public sealed class ProjectColorPaletteTests
{
    [Fact]
    public void Same_project_always_gets_the_same_color()
    {
        ProjectColorPalette.ColorFor("proyecto-alfa").ShouldBe(ProjectColorPalette.ColorFor("proyecto-alfa"));
    }

    [Theory]
    [InlineData("Proyecto-Alfa")]
    [InlineData("  proyecto-alfa  ")]
    [InlineData("PROYECTO-ALFA")]
    public void Color_ignores_case_and_surrounding_spaces(string variant)
    {
        ProjectColorPalette.ColorFor(variant).ShouldBe(ProjectColorPalette.ColorFor("proyecto-alfa"));
    }

    [Fact]
    public void Color_is_stable_across_releases()
    {
        // Si cambia el algoritmo, los usuarios verían otros colores: este valor fija el contrato.
        ProjectColorPalette.ColorFor("proyecto-alfa").ShouldBe(ProjectColorPalette.Colors[Expected("proyecto-alfa")]);
    }

    [Fact]
    public void Colors_are_reasonably_distributed()
    {
        var used = Enumerable.Range(0, 200)
            .Select(i => ProjectColorPalette.ColorFor($"proyecto-ficticio-{i}"))
            .Distinct(StringComparer.Ordinal)
            .Count();

        used.ShouldBe(ProjectColorPalette.Colors.Count);
    }

    [Fact]
    public void Empty_name_still_gets_a_valid_color()
    {
        ProjectColorPalette.Colors.Contains(ProjectColorPalette.ColorFor(null), StringComparer.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void Project_key_normalizes_composed_and_decomposed_accents()
    {
        // "é" precompuesta (U+00E9) frente a "e" + acento combinante (U+0301).
        ProjectKey.Normalize("caf\u00E9").ShouldBe(ProjectKey.Normalize("cafe\u0301"));
    }

    /// <summary>Referencia independiente de FNV-1a de 32 bits.</summary>
    private static int Expected(string key)
    {
        var hash = 2_166_136_261u;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(key))
            hash = (hash ^ b) * 16_777_619u;

        return (int)(hash % (uint)ProjectColorPalette.Colors.Count);
    }
}
