using TabDock.Core.Domain;
using TabDock.Core.TitleParsing;

namespace TabDock.Core.Tests.TitleParsing;

/// <summary>Títulos ficticios con la plantilla por defecto de VS Code y sus variantes.</summary>
public sealed class VsCodeTitleParserTests
{
    private readonly VsCodeTitleParser _parser = new();

    [Theory]
    [InlineData("index.ts - proyecto-alfa - Visual Studio Code", "proyecto-alfa", "index.ts")]
    [InlineData("proyecto-alfa - Visual Studio Code", "proyecto-alfa", null)]
    [InlineData("Welcome - proyecto-beta - Visual Studio Code", "proyecto-beta", "Welcome")]
    [InlineData("Bienvenida - proyecto-beta - Visual Studio Code", "proyecto-beta", "Bienvenida")]
    [InlineData("settings.json - mi.app.web - Visual Studio Code", "mi.app.web", "settings.json")]
    [InlineData("index.ts - proyecto-alfa - Visual Studio Code - Insiders", "proyecto-alfa", "index.ts")]
    [InlineData("index.ts - proyecto-alfa - VSCodium", "proyecto-alfa", "index.ts")]
    [InlineData("index.ts - proyecto-alfa - Cursor", "proyecto-alfa", "index.ts")]
    [InlineData("index.ts - proyecto-alfa - EditorDerivadoFicticio", "proyecto-alfa", "index.ts")]
    [InlineData("informe - final.md - proyecto-gamma - Visual Studio Code", "proyecto-gamma", "informe - final.md")]
    [InlineData("a - b - c.txt - proyecto-delta - Visual Studio Code", "proyecto-delta", "a - b - c.txt")]
    public void Extracts_project_and_active_file(string title, string project, string? file)
    {
        var parsed = _parser.Parse(title);

        parsed.ProjectName.ShouldBe(project);
        parsed.ActiveFile.ShouldBe(file);
        parsed.IsEmptyWindow.ShouldBeFalse();
    }

    [Theory]
    [InlineData("● index.ts - proyecto-alfa - Visual Studio Code", true)]
    [InlineData("●index.ts - proyecto-alfa - Visual Studio Code", true)]
    [InlineData("index.ts - proyecto-alfa - Visual Studio Code", false)]
    public void Detects_unsaved_changes(string title, bool dirty)
    {
        var parsed = _parser.Parse(title);

        parsed.IsDirty.ShouldBe(dirty);
        parsed.ProjectName.ShouldBe("proyecto-alfa");
        parsed.ActiveFile.ShouldBe("index.ts");
    }

    [Theory]
    [InlineData("server.ts - demo-api [SSH: servidor-ficticio] - Visual Studio Code", "demo-api", RemoteKind.Ssh, "servidor-ficticio", "server.ts")]
    [InlineData("main.py - servicio-ficticio [WSL: Ubuntu] - Visual Studio Code", "servicio-ficticio", RemoteKind.Wsl, "Ubuntu", "main.py")]
    [InlineData("compose.yml - panel-demo [Dev Container: panel-demo] - Visual Studio Code", "panel-demo", RemoteKind.DevContainer, "panel-demo", "compose.yml")]
    [InlineData("app.js - web-ficticia [Codespaces: espacio-ficticio] - Visual Studio Code", "web-ficticia", RemoteKind.Other, "espacio-ficticio", "app.js")]
    [InlineData("demo-api [SSH: servidor-ficticio] - Visual Studio Code", "demo-api", RemoteKind.Ssh, "servidor-ficticio", null)]
    public void Detects_remote_environments(string title, string project, RemoteKind kind, string label, string? file)
    {
        var parsed = _parser.Parse(title);

        parsed.ProjectName.ShouldBe(project);
        parsed.RemoteKind.ShouldBe(kind);
        parsed.RemoteLabel.ShouldBe(label);
        parsed.ActiveFile.ShouldBe(file);
    }

    [Fact]
    public void Remote_marker_decides_the_root_even_with_a_profile()
    {
        var parsed = _parser.Parse("api.ts - demo-api [SSH: servidor-ficticio] - Perfil Web - Visual Studio Code");

        parsed.ProjectName.ShouldBe("demo-api");
        parsed.ActiveFile.ShouldBe("api.ts");
        parsed.ProfileName.ShouldBe("Perfil Web");
    }

    [Fact]
    public void Reads_the_profile_name()
    {
        var parsed = _parser.Parse("index.ts - proyecto-alfa - Perfil Web - Visual Studio Code");

        parsed.ProjectName.ShouldBe("proyecto-alfa");
        parsed.ActiveFile.ShouldBe("index.ts");
        parsed.ProfileName.ShouldBe("Perfil Web");
    }

    [Theory]
    [InlineData("Visual Studio Code", null)]
    [InlineData("● Visual Studio Code", null)]
    [InlineData("README.md - Visual Studio Code", "README.md")]
    [InlineData("Untitled-1 - Visual Studio Code", "Untitled-1")]
    [InlineData("● Sin título-2 - Visual Studio Code", "Sin título-2")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Windows_without_a_folder_are_empty(string? title, string? file)
    {
        var parsed = _parser.Parse(title);

        parsed.IsEmptyWindow.ShouldBeTrue();
        parsed.ActiveFile.ShouldBe(file);
    }

    [Theory]
    [InlineData("index.ts - proyecto-alfa - Visual Studio Code [Administrador]")]
    [InlineData("index.ts - proyecto-alfa - Visual Studio Code [Unsupported]")]
    [InlineData("index.ts - proyecto-alfa [Modo restringido] - Visual Studio Code")]
    public void Ignores_non_remote_tags(string title)
    {
        var parsed = _parser.Parse(title);

        parsed.ProjectName.ShouldBe("proyecto-alfa");
        parsed.RemoteKind.ShouldBe(RemoteKind.None);
    }

    [Fact]
    public void Custom_separator_is_supported()
    {
        var parsed = new VsCodeTitleParser(separator: " — ").Parse("● index.ts — proyecto-alfa — Visual Studio Code");

        parsed.ProjectName.ShouldBe("proyecto-alfa");
        parsed.ActiveFile.ShouldBe("index.ts");
        parsed.IsDirty.ShouldBeTrue();
    }

    [Theory]
    [InlineData("proyecto-alfa - index.ts - Visual Studio Code", "proyecto-alfa", "index.ts")]
    [InlineData("proyecto-alfa - Visual Studio Code", "proyecto-alfa", null)]
    [InlineData("● proyecto-alfa - informe - final.md - Visual Studio Code", "proyecto-alfa", "informe - final.md")]
    public void Recommended_template_puts_the_root_first(string title, string project, string? file)
    {
        var parsed = new VsCodeTitleParser(VsCodeTitleTemplate.RootFirst).Parse(title);

        parsed.ProjectName.ShouldBe(project);
        parsed.ActiveFile.ShouldBe(file);
    }

    [Fact]
    public void Unicode_names_are_kept_intact()
    {
        var parsed = _parser.Parse("índice.md - proyecto-ñandú 🚀 - Visual Studio Code");

        parsed.ProjectName.ShouldBe("proyecto-ñandú 🚀");
        parsed.ActiveFile.ShouldBe("índice.md");
    }

    [Fact]
    public void Very_long_titles_do_not_break_parsing()
    {
        var longName = new string('x', 2_000);

        var parsed = _parser.Parse($"{longName}.ts - proyecto-alfa - Visual Studio Code");

        parsed.ProjectName.ShouldBe("proyecto-alfa");
    }
}
