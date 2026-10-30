using System.IO.Compression;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using TabDock.Core.Diagnostics;
using TabDock.Core.Domain;
using TabDock.Infrastructure.Diagnostics;

namespace TabDock.Infrastructure.Tests.Diagnostics;

/// <summary>Usuario, equipo y nombres son ficticios.</summary>
public sealed class DiagnosticsExporterTests : IDisposable
{
    private const string FakeUser = "usuario-ficticio";
    private const string FakeMachine = "EQUIPO-DEMO";
    private const string FakeProfile = @"C:\Users\usuario-ficticio";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tabdock-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly DiagnosticsExporter _exporter;
    private readonly DiagnosticsScrubber _scrubber = new(new MaskingContext(FakeProfile, FakeUser), FakeMachine, FakeMachine);
    private readonly DiagnosticsEnvironment _environment = new("1.0.0", "10.0.26200.0", "X64", "es-ES", [new DisplayInfo(2560, 1440, 120, IsPrimary: true)]);

    public DiagnosticsExporterTests()
    {
        _paths = new AppPaths(_root);
        _paths.EnsureCreated();
        _exporter = new DiagnosticsExporter(_paths, TimeProvider.System, NullLogger<DiagnosticsExporter>.Instance);
    }

    private string ZipPath => Path.Combine(_root, "diagnostico.zip");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void The_zip_contains_readme_system_settings_and_logs_only()
    {
        File.WriteAllText(Path.Combine(_paths.Logs, "tabdock-20261030.log"), "{\"@mt\":\"TabDock iniciado\"}");
        File.WriteAllText(_paths.PreferencesFile, "{\"alias\":\"cliente-ficticio\"}");

        _exporter.Export(ZipPath, Settings(), _environment, _scrubber).IsSuccess.ShouldBeTrue();

        using var zip = ZipFile.OpenRead(ZipPath);
        zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ShouldBe(["LEEME.txt", "logs/tabdock-20261030.log", "settings.json", "system.json"]);
        File.Exists(ZipPath + ".tmp").ShouldBeFalse();
    }

    [Fact]
    public void Rule_names_and_path_patterns_are_anonymized_but_programs_and_hotkeys_are_kept()
    {
        _exporter.Export(ZipPath, Settings(), _environment, _scrubber).IsSuccess.ShouldBeTrue();

        var settings = Read("settings.json");
        settings.ShouldNotContain("Proyecto cliente-ficticio");
        settings.ShouldNotContain(FakeUser);
        settings.ShouldContain("regla-1");
        settings.ShouldContain("Code.exe");
        settings.ShouldContain("MergeAll");
    }

    [Fact]
    public void Logs_get_a_second_masking_pass()
    {
        var leaked = $$"""{"@mt":"Ruta {Path}","Path":"C:\\Users\\{{FakeUser}}\\app","Host":"{{FakeMachine}}","Text":"sesión de {{FakeUser}}"}""";
        File.WriteAllText(Path.Combine(_paths.Logs, "tabdock-20261030.log"), leaked);

        _exporter.Export(ZipPath, Settings(), _environment, _scrubber).IsSuccess.ShouldBeTrue();

        var log = Read("logs/tabdock-20261030.log");
        log.ShouldNotContain(FakeUser, Case.Insensitive);
        log.ShouldNotContain(FakeMachine, Case.Insensitive);
        log.ShouldContain(LogMask.UserProfileToken);
        log.ShouldContain(DiagnosticsScrubber.MachineToken);
    }

    [Fact]
    public void System_info_has_versions_and_display_scale()
    {
        _exporter.Export(ZipPath, Settings(), _environment, _scrubber).IsSuccess.ShouldBeTrue();

        var system = JsonNode.Parse(Read("system.json"))!;
        system["osVersion"]!.GetValue<string>().ShouldBe("10.0.26200.0");
        system["displays"]![0]!["scalePercent"]!.GetValue<int>().ShouldBe(125);
    }

    [Fact]
    public void An_unwritable_destination_fails_without_throwing()
    {
        var result = _exporter.Export(Path.Combine(_root, "no-existe", "diag.zip"), Settings(), _environment, _scrubber);

        result.IsSuccess.ShouldBeFalse();
        result.Error.MessageKey.ShouldBe("Error_DiagnosticsExport");
    }

    [Theory]
    [InlineData("mañana")]
    [InlineData("anaconda")]
    public void Short_or_partial_matches_are_not_replaced(string word)
    {
        var scrubber = new DiagnosticsScrubber(new MaskingContext(UserProfile: null, UserName: "ana"), machineName: null, domainName: null);

        scrubber.Scrub($"texto {word} y ana").ShouldBe($"texto {word} y {LogMask.UserNameToken}");
    }

    private static SettingsDocument Settings()
    {
        var settings = SettingsDocument.CreateDefault(DateTimeOffset.UnixEpoch);
        return settings with
        {
            Rules = [settings.Rules[0] with { DisplayName = "Proyecto cliente-ficticio", ExecutablePathPattern = @"C:\Users\usuario-ficticio\*" }],
        };
    }

    private string Read(string entry)
    {
        using var zip = ZipFile.OpenRead(ZipPath);
        using var reader = new StreamReader(zip.GetEntry(entry)!.Open());
        return reader.ReadToEnd();
    }
}
