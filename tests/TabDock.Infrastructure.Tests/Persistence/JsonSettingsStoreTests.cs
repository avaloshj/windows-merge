using Microsoft.Extensions.Logging.Abstractions;
using TabDock.Core.Domain;
using TabDock.Infrastructure.Persistence;

namespace TabDock.Infrastructure.Tests.Persistence;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tabdock-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests()
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
    public void First_run_creates_and_saves_the_defaults()
    {
        var settings = _store.Load();

        settings.Rules.Count.ShouldBe(4);
        File.Exists(_paths.SettingsFile).ShouldBeTrue();
        _store.Load().Rules.Select(r => r.Id).ShouldBe(settings.Rules.Select(r => r.Id));
    }

    [Fact]
    public void Changes_round_trip_and_enums_are_stored_as_text()
    {
        var settings = _store.Load();
        var changed = settings with { Rules = [settings.Rules[0] with { MergeOnStartup = true, SidebarPosition = SidebarPosition.Right }] };

        _store.Save(changed);

        var loaded = _store.Load();
        loaded.Rules.Single().MergeOnStartup.ShouldBeTrue();
        loaded.Rules.Single().SidebarPosition.ShouldBe(SidebarPosition.Right);
        File.ReadAllText(_paths.SettingsFile).ShouldContain("\"VsCode\"");
    }

    [Fact]
    public void Missing_hotkeys_are_completed_with_defaults()
    {
        var settings = _store.Load();
        _store.Save(settings with { Hotkeys = [settings.Hotkeys[0]] });

        _store.Load().Hotkeys.Count.ShouldBe(HotkeyBinding.Defaults.Count);
    }

    [Theory]
    [InlineData("{ roto")]
    [InlineData("""{ "schemaVersion": 1, "desconocido": 1 }""")]
    [InlineData("""{ "schemaVersion": 42 }""")]
    public void Corrupt_settings_are_quarantined_and_replaced_by_defaults(string contents)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_paths.SettingsFile, contents);

        var settings = _store.Load();

        settings.Rules.Count.ShouldBe(4);
        Directory.GetFiles(_root, "settings.json.corrupt-*").Length.ShouldBe(1);
    }
}
