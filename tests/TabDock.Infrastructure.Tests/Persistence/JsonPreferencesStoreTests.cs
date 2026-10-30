using Microsoft.Extensions.Logging.Abstractions;
using TabDock.Core.Domain;
using TabDock.Infrastructure.Persistence;

namespace TabDock.Infrastructure.Tests.Persistence;

public sealed class JsonPreferencesStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tabdock-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly JsonPreferencesStore _store;

    public JsonPreferencesStoreTests()
    {
        _paths = new AppPaths(_root);
        _store = new JsonPreferencesStore(_paths, TimeProvider.System, NullLogger<JsonPreferencesStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Missing_file_loads_as_empty()
    {
        _store.Load().Tabs.ShouldBeEmpty();
    }

    [Fact]
    public void Preferences_round_trip()
    {
        var preference = new TabPreference
        {
            RuleId = Guid.NewGuid(),
            ProjectKey = "proyecto-alfa",
            Alias = "Alfa",
            ColorHex = "#EC4899",
            Pinned = true,
            Order = 2,
            LastSeenAt = new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero),
        };

        _store.Save(new PreferencesDocument { Tabs = [preference] });

        _store.Load().Tabs.Single().ShouldBe(preference);
    }

    [Fact]
    public void Corrupt_file_is_quarantined()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_paths.PreferencesFile, "{ roto");

        _store.Load().Tabs.ShouldBeEmpty();
        Directory.GetFiles(_root, "preferences.json.corrupt-*").Length.ShouldBe(1);
    }
}
