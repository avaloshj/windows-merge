using Microsoft.Extensions.Logging.Abstractions;
using TabDock.Core.Abstractions;

namespace TabDock.Win32.IntegrationTests;

/// <summary>Captura, ocultación, colocación y restauración sobre ventanas reales de TestTarget.</summary>
[Trait("Category", "Integration")]
public sealed class WindowOperationsTests
{
    private readonly WindowSystem _windows = new(NullLogger<WindowSystem>.Instance, TimeProvider.System);

    [Fact]
    public async Task Hidden_window_is_restored_exactly_where_it_was()
    {
        using var target = TestTargetProcess.Start("--title", Unique("proyecto-alfa"), "--size", "760x540");
        var window = await target.WaitForWindowAsync(_windows, _ => true);
        var snapshot = _windows.CaptureSnapshot(window.Handle).Value!;

        _windows.Hide(window.Handle).IsSuccess.ShouldBeTrue();
        _windows.GetInfo(window.Handle).Value!.IsVisible.ShouldBeFalse();

        _windows.Restore(snapshot).IsSuccess.ShouldBeTrue();
        var restored = _windows.GetInfo(window.Handle).Value!;
        restored.IsVisible.ShouldBeTrue();
        restored.ExtendedFrameBounds.ShouldBe(window.ExtendedFrameBounds);
    }

    [Fact]
    public async Task Place_in_slot_matches_the_visible_area_exactly()
    {
        using var target = TestTargetProcess.Start("--title", Unique("proyecto-beta"));
        var window = await target.WaitForWindowAsync(_windows, _ => true);
        var slot = new PixelRect(window.ExtendedFrameBounds.Left + 40, window.ExtendedFrameBounds.Top + 30, window.ExtendedFrameBounds.Left + 940, window.ExtendedFrameBounds.Top + 630);

        _windows.PlaceInSlot(window.Handle, slot).IsSuccess.ShouldBeTrue();

        _windows.GetInfo(window.Handle).Value!.ExtendedFrameBounds.ShouldBe(slot);
    }

    [Fact]
    public async Task Maximized_window_is_placed_as_normal_and_restored_maximized()
    {
        using var target = TestTargetProcess.Start("--title", Unique("demo-api"), "--maximized");
        var window = await target.WaitForWindowAsync(_windows, w => w.ShowState == WindowShowState.Maximized);
        var snapshot = _windows.CaptureSnapshot(window.Handle).Value!;
        snapshot.WasMaximized.ShouldBeTrue();
        var slot = new PixelRect(200, 150, 1000, 750);

        _windows.PlaceInSlot(window.Handle, slot).IsSuccess.ShouldBeTrue();
        var placed = _windows.GetInfo(window.Handle).Value!;
        placed.ShowState.ShouldBe(WindowShowState.Normal);
        placed.ExtendedFrameBounds.ShouldBe(slot);

        _windows.Restore(snapshot).IsSuccess.ShouldBeTrue();
        _windows.GetInfo(window.Handle).Value!.ShowState.ShouldBe(WindowShowState.Maximized);
    }

    [Fact]
    public async Task Identity_stops_matching_when_the_process_ends_and_restore_is_refused()
    {
        WindowHandle handle;
        Core.Domain.WindowSnapshot snapshot;
        using (var target = TestTargetProcess.Start("--title", Unique("proyecto-gamma")))
        {
            var window = await target.WaitForWindowAsync(_windows, _ => true);
            handle = window.Handle;
            snapshot = _windows.CaptureSnapshot(handle).Value!;
            snapshot.Identity.ProcessStartTime.ShouldNotBeNull();
            _windows.Matches(snapshot.Identity).ShouldBeTrue();
        }

        _windows.Matches(snapshot.Identity).ShouldBeFalse();
        _windows.Restore(snapshot).Error!.Code.ShouldBe("window.identityMismatch");
    }

    [Fact]
    public async Task Request_close_closes_the_window()
    {
        using var target = TestTargetProcess.Start("--title", Unique("proyecto-delta"));
        var window = await target.WaitForWindowAsync(_windows, _ => true);

        _windows.RequestClose(window.Handle).IsSuccess.ShouldBeTrue();

        target.Process.WaitForExit(5_000).ShouldBeTrue();
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}
