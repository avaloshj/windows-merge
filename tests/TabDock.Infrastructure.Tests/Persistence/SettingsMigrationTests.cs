using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using TabDock.Core.Domain;
using TabDock.Infrastructure.Persistence;

namespace TabDock.Infrastructure.Tests.Persistence;

public sealed class SettingsMigrationTests : IDisposable
{
    /// <summary>settings.json tal como lo escribía la Fase 4 (esquema 1), con una regla ficticia.</summary>
    private const string Version1 = """
        {
          "schemaVersion": 1,
          "rules": [
            {
              "id": "6f1c1d2e-0000-4000-8000-000000000001",
              "displayName": "Visual Studio Code",
              "executableName": "Code.exe",
              "executablePathPattern": null,
              "windowClassName": "Chrome_WidgetWin_1",
              "titleParser": "VsCode",
              "enabled": true,
              "autoMerge": false,
              "mergeOnStartup": true,
              "sidebarPosition": "Left",
              "createdAt": "2026-10-01T00:00:00+00:00",
              "updatedAt": "2026-10-01T00:00:00+00:00"
            }
          ],
          "hotkeys": []
        }
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tabdock-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _store;

    public SettingsMigrationTests()
    {
        _paths = new AppPaths(_root);
        _store = new JsonSettingsStore(_paths, TimeProvider.System, NullLogger<JsonSettingsStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Version_1_gets_a_user_section_and_existing_users_skip_the_introduction()
    {
        var migrated = SettingsMigrator.MigrateToCurrent(JsonNode.Parse(Version1)!.AsObject());

        SettingsMigrator.ReadVersion(migrated).ShouldBe(SettingsDocument.CurrentSchemaVersion);
        migrated["user"]!["onboardingCompleted"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void A_file_without_schema_version_is_treated_as_version_1()
    {
        SettingsMigrator.ReadVersion(new JsonObject()).ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(SettingsDocument.CurrentSchemaVersion + 1)]
    public void Unknown_versions_are_not_migrated(int version)
    {
        Should.Throw<NotSupportedException>(() => SettingsMigrator.MigrateToCurrent(new JsonObject { ["schemaVersion"] = version }));
    }

    [Fact]
    public void Loading_a_version_1_file_keeps_rules_and_rewrites_it_as_the_current_version()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_paths.SettingsFile, Version1);

        var settings = _store.Load();

        settings.SchemaVersion.ShouldBe(SettingsDocument.CurrentSchemaVersion);
        var rule = settings.Rules.Single();
        rule.DisplayName.ShouldBe("Visual Studio Code");
        rule.AutoMerge.ShouldBeFalse();
        rule.MergeOnStartup.ShouldBeTrue();
        settings.User.OnboardingCompleted.ShouldBeTrue();
        settings.User.ShouldBe(new UserSettings { OnboardingCompleted = true });
        settings.Hotkeys.Count.ShouldBe(HotkeyBinding.Defaults.Count);

        var onDisk = JsonNode.Parse(File.ReadAllText(_paths.SettingsFile))!;
        onDisk["schemaVersion"]!.GetValue<int>().ShouldBe(SettingsDocument.CurrentSchemaVersion);
        Directory.GetFiles(_root, "settings.json.corrupt-*").ShouldBeEmpty();
    }

    [Fact]
    public void A_partial_user_section_keeps_the_default_values_of_missing_fields()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_paths.SettingsFile, """{ "schemaVersion": 2, "user": { "theme": "Dark" }, "rules": [], "hotkeys": [] }""");

        var user = _store.Load().User;

        user.ShouldBe(new UserSettings { Theme = ThemePreference.Dark });
    }

    [Fact]
    public void Rules_and_hotkeys_edited_by_hand_keep_the_default_values_of_missing_fields()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_paths.SettingsFile, """
            {
              "schemaVersion": 2,
              "rules": [ { "id": "6f1c1d2e-0000-4000-8000-000000000002", "displayName": "Programa de prueba", "executableName": "demo.exe",
                           "createdAt": "2026-10-01T00:00:00+00:00", "updatedAt": "2026-10-01T00:00:00+00:00" } ],
              "hotkeys": [ { "command": "MergeAll", "modifiers": "Alt, Control", "virtualKey": 77 } ]
            }
            """);

        var settings = _store.Load();

        var rule = settings.Rules.Single();
        rule.Enabled.ShouldBeTrue();
        rule.AutoMerge.ShouldBeTrue();
        rule.MergeOnStartup.ShouldBeFalse();
        rule.TitleParser.ShouldBe(TitleParserKind.Generic);
        settings.Hotkeys.Single(h => h.Command == HotkeyCommand.MergeAll).Enabled.ShouldBeTrue();
    }

    [Fact]
    public void New_installations_start_with_the_introduction_pending()
    {
        _store.Load().User.OnboardingCompleted.ShouldBeFalse();
    }

    [Fact]
    public void User_settings_round_trip()
    {
        var settings = _store.Load();
        _store.Save(settings with { User = settings.User with { Theme = ThemePreference.Dark, Density = Density.Compact, Language = "en", HealthCheckSeconds = 4 } });

        var user = _store.Load().User;

        user.Theme.ShouldBe(ThemePreference.Dark);
        user.Density.ShouldBe(Density.Compact);
        user.Language.ShouldBe("en");
        user.HealthCheckSeconds.ShouldBe(4);
        File.ReadAllText(_paths.SettingsFile).ShouldContain("\"Compact\"");
    }
}
