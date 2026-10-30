using Microsoft.Extensions.Logging.Abstractions;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;
using TabDock.Core.Services;
using TabDock.Core.Tests.Fakes;

namespace TabDock.Core.Tests.Services;

public sealed class RecoveryTests
{
    private static readonly PixelRect s_original = new(50, 60, 850, 660);

    private readonly List<string> _journal = [];
    private readonly FakeWindowSystem _windows;
    private readonly FakeStateStore _store;

    public RecoveryTests()
    {
        _windows = new FakeWindowSystem(_journal);
        _store = new FakeStateStore(_journal);
    }

    [Fact]
    public void Tracker_persists_every_change_and_marks_clean_when_empty()
    {
        var tracker = new RuntimeStateTracker(_store, ownerProcessId: 42);
        var snapshot = Snapshot(_windows.Add(1, "proyecto-alfa", s_original));

        tracker.Track(Guid.NewGuid(), snapshot).IsSuccess.ShouldBeTrue();
        _store.Current.Windows.Count.ShouldBe(1);
        _store.Current.CleanShutdown.ShouldBeFalse();
        _store.Current.OwnerProcessId.ShouldBe(42);

        tracker.Untrack(snapshot.Identity.Handle);
        _store.Current.Windows.ShouldBeEmpty();
        _store.Current.CleanShutdown.ShouldBeTrue();
    }

    [Fact]
    public void Tracker_rolls_back_when_the_state_cannot_be_written()
    {
        var tracker = new RuntimeStateTracker(_store, ownerProcessId: 42);
        _store.FailSave = true;

        var result = tracker.Track(Guid.NewGuid(), Snapshot(_windows.Add(1, "proyecto-alfa", s_original)));

        result.IsSuccess.ShouldBeFalse();
        tracker.Count.ShouldBe(0);
    }

    [Fact]
    public void Pending_windows_are_restored_and_the_state_is_cleared()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);
        var beta = _windows.Add(2, "proyecto-beta", s_original);
        var snapshots = new[] { Snapshot(alfa), Snapshot(beta) };
        alfa.Visible = beta.Visible = false;
        alfa.Bounds = beta.Bounds = new PixelRect(300, 140, 1500, 900);
        _store.Current = new RuntimeState { CleanShutdown = false, Windows = [.. snapshots.Select(s => new CapturedWindowRecord(Guid.NewGuid(), s))] };

        var report = CreateRestorer().RestorePending();

        report.ShouldBe(new RecoveryReport(Pending: 2, Restored: 2, Skipped: 0, Failed: 0));
        alfa.Visible.ShouldBeTrue();
        alfa.Bounds.ShouldBe(s_original);
        beta.Visible.ShouldBeTrue();
        _store.Current.ShouldBe(RuntimeState.Empty);
    }

    [Fact]
    public void Windows_that_no_longer_exist_are_skipped()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);
        var snapshot = Snapshot(alfa);
        alfa.Alive = false;
        _store.Current = new RuntimeState { Windows = [new CapturedWindowRecord(Guid.NewGuid(), snapshot)] };

        var report = CreateRestorer().RestorePending();

        report.Skipped.ShouldBe(1);
        _journal.ShouldNotContain("restore:1");
    }

    [Fact]
    public void A_reused_handle_from_another_process_is_never_restored()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);
        var foreign = Snapshot(alfa) with { Identity = new WindowIdentity(alfa.Handle, ProcessId: 5555, null) };
        _store.Current = new RuntimeState { Windows = [new CapturedWindowRecord(Guid.NewGuid(), foreign)] };

        var report = CreateRestorer().RestorePending();

        report.Skipped.ShouldBe(1);
        _journal.ShouldNotContain("restore:1");
    }

    [Fact]
    public void Guardian_does_not_restore_state_owned_by_another_instance()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);
        var snapshot = Snapshot(alfa);
        alfa.Visible = false;
        _store.Current = new RuntimeState { OwnerProcessId = 2222, Windows = [new CapturedWindowRecord(Guid.NewGuid(), snapshot)] };

        var report = CreateRestorer().RestorePending(onlyOwner: 1111);

        report.Pending.ShouldBe(0);
        alfa.Visible.ShouldBeFalse();
        _store.Current.Windows.Count.ShouldBe(1);
    }

    [Fact]
    public void Nothing_pending_means_no_writes()
    {
        CreateRestorer().RestorePending().Pending.ShouldBe(0);

        _store.Saves.ShouldBe(0);
    }

    private SnapshotRestorer CreateRestorer() => new(_windows, _store, NullLogger<SnapshotRestorer>.Instance);

    private WindowSnapshot Snapshot(FakeWindow window) => _windows.CaptureSnapshot(window.Handle).Value!;
}
