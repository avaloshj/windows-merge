using TabDock.App.Hosting;
using TabDock.Core.Domain;

namespace TabDock.App.Tests.Hosting;

public sealed class CommandLineOptionsTests
{
    [Fact]
    public void Without_arguments_nothing_overrides_the_saved_settings()
    {
        var options = CommandLineOptions.Parse([]);

        options.Language.ShouldBeNull();
        options.Theme.ShouldBeNull();
        options.UseMica.ShouldBeNull();
        options.Verbose.ShouldBeFalse();
        options.DemoFrame.ShouldBeFalse();
        options.Settings.ShouldBeFalse();
        options.ExportDiagnosticsTo.ShouldBeNull();
    }

    [Fact]
    public void Accepts_all_options_case_insensitively()
    {
        var options = CommandLineOptions.Parse(["--LANG", "EN", "--verbose", "--theme", "Dark", "--no-mica", "--demo-frame", "--Settings", "--onboarding"]);

        options.Language.ShouldBe("en");
        options.Verbose.ShouldBeTrue();
        options.Theme.ShouldBe(ThemePreference.Dark);
        options.UseMica.ShouldBe(false);
        options.DemoFrame.ShouldBeTrue();
        options.Settings.ShouldBeTrue();
        options.Onboarding.ShouldBeTrue();
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    public void Unsupported_language_is_ignored(string language)
    {
        CommandLineOptions.Parse(["--lang", language]).Language.ShouldBeNull();
    }

    [Theory]
    [InlineData("sepia")]
    [InlineData("7")]
    public void Unknown_theme_is_ignored(string theme)
    {
        CommandLineOptions.Parse(["--theme", theme]).Theme.ShouldBeNull();
    }

    [Fact]
    public void Merge_executable_and_export_path_are_read()
    {
        CommandLineOptions.Parse(["--merge-exe", "Code.exe"]).MergeExecutable.ShouldBe("Code.exe");
        CommandLineOptions.Parse(["--merge-exe", "--verbose"]).MergeExecutable.ShouldBeNull();
        CommandLineOptions.Parse(["--export-diagnostics", @"C:\temp\diag.zip"]).ExportDiagnosticsTo.ShouldBe(@"C:\temp\diag.zip");
        CommandLineOptions.Parse(["--export-diagnostics"]).ExportDiagnosticsTo.ShouldBeNull();
    }

    [Fact]
    public void Guardian_mode_requires_process_id_and_start_time()
    {
        CommandLineOptions.Parse(["--guardian", "4321", "638000000000000000"]).GuardianOf.ShouldBe((4321, 638000000000000000L));
        CommandLineOptions.Parse(["--guardian", "4321"]).GuardianOf.ShouldBeNull();
        CommandLineOptions.Parse(["--guardian", "x", "1"]).GuardianOf.ShouldBeNull();
    }

    [Fact]
    public void Options_without_value_are_ignored()
    {
        var options = CommandLineOptions.Parse(["--lang", "--theme"]);

        options.Language.ShouldBeNull();
        options.Theme.ShouldBeNull();
    }
}
