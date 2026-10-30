using Microsoft.Extensions.Logging.Abstractions;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;
using TabDock.Infrastructure.Persistence;

namespace TabDock.Infrastructure.Tests.Persistence;

public sealed class JsonRuntimeStateStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tabdock-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly JsonRuntimeStateStore _store;

    public JsonRuntimeStateStoreTests()
    {
        _paths = new AppPaths(_root);
        _store = new JsonRuntimeStateStore(_paths, TimeProvider.System, NullLogger<JsonRuntimeStateStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Missing_file_loads_as_empty()
    {
        _store.Load().ShouldBe(RuntimeState.Empty);
    }

    [Fact]
    public void State_round_trips_exactly()
    {
        var state = SampleState();

        _store.Save(state);
        var loaded = _store.Load();

        loaded.OwnerProcessId.ShouldBe(state.OwnerProcessId);
        loaded.CleanShutdown.ShouldBeFalse();
        loaded.Windows.Count.ShouldBe(1);
        loaded.Windows[0].ShouldBe(state.Windows[0]);
    }

    [Fact]
    public void Saving_leaves_no_temporary_file_and_overwrites_previous_state()
    {
        _store.Save(SampleState());
        _store.Save(RuntimeState.Empty);

        File.Exists(_paths.StateFile + ".tmp").ShouldBeFalse();
        _store.Load().Windows.ShouldBeEmpty();
    }

    [Fact]
    public void State_file_contains_no_titles_or_paths()
    {
        _store.Save(SampleState());

        var json = File.ReadAllText(_paths.StateFile);
        json.ShouldNotContain("proyecto");
        json.ShouldNotContain(@"\\");
    }

    [Theory]
    [InlineData("{ esto no es json")]
    [InlineData("""{ "schemaVersion": 1, "campoDesconocido": true }""")]
    [InlineData("""{ "schemaVersion": 99, "windows": [] }""")]
    public void Unreadable_file_is_quarantined_and_loads_as_empty(string contents)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_paths.StateFile, contents);

        var loaded = _store.Load();

        loaded.ShouldBe(RuntimeState.Empty);
        File.Exists(_paths.StateFile).ShouldBeFalse();
        Directory.GetFiles(_root, "state.json.corrupt-*").Length.ShouldBe(1);
    }

    private static RuntimeState SampleState() => new()
    {
        OwnerProcessId = 4321,
        CleanShutdown = false,
        Windows =
        [
            new CapturedWindowRecord(
                Guid.Parse("5d2c1b8e-0000-4000-8000-000000000001"),
                new WindowSnapshot(
                    new WindowIdentity(new WindowHandle(0x1A2B3C), 1000, new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero)),
                    new WindowPlacementData(2, 1, -1, -1, -1, -1, new PixelRect(10, 20, 810, 620)),
                    new PixelRect(10, 20, 810, 620),
                    WasVisible: true,
                    WasMaximized: false,
                    WasMinimized: false,
                    new DateTimeOffset(2026, 10, 30, 9, 30, 0, TimeSpan.Zero))),
        ],
    };
}
