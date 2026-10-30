using Microsoft.Extensions.Logging.Abstractions;
using TabDock.Core.Abstractions;
using TabDock.Core.Services;

namespace TabDock.Win32.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class WindowSystemTests
{
    private readonly WindowSystem _windowSystem = new(NullLogger<WindowSystem>.Instance, TimeProvider.System);
    private readonly EligibilityPolicy _policy = new(new EligibilityOptions(), Environment.ProcessId);

    [Fact]
    public async Task Test_target_window_is_enumerated_with_metadata_and_is_eligible()
    {
        var title = UniqueTitle("proyecto-alfa");
        using var target = TestTargetProcess.Start("--title", title, "--size", "800x600");

        var window = await target.WaitForWindowAsync(_windowSystem, w => HasTitle(w, title));

        window.ExecutableName.ShouldBe("TabDock.TestTarget.exe");
        window.ClassName.ShouldStartWith("HwndWrapper[TabDock.TestTarget");
        window.ProcessStartTime.ShouldNotBeNull();
        window.IsProcessAccessible.ShouldBeTrue();
        window.IsElevatedProcess.ShouldBe(Environment.IsPrivilegedProcess);
        window.ShowState.ShouldBe(WindowShowState.Normal);
        window.ExtendedFrameBounds.Width.ShouldBeGreaterThan(300);
        _policy.Evaluate(window).ShouldBe(EligibilityResult.Eligible);
    }

    [Fact]
    public async Task Tool_window_is_excluded()
    {
        var title = UniqueTitle("herramienta-ficticia");
        using var target = TestTargetProcess.Start("--title", title, "--tool-window");

        var window = await target.WaitForWindowAsync(_windowSystem, w => HasTitle(w, title));

        window.IsToolWindow.ShouldBeTrue();
        _policy.Evaluate(window).ShouldBe(EligibilityResult.Excluded(ExclusionReason.ToolWindow));
    }

    [Fact]
    public async Task Small_window_is_excluded()
    {
        var title = UniqueTitle("ventana-pequena");
        using var target = TestTargetProcess.Start("--title", title, "--size", "250x150");

        var window = await target.WaitForWindowAsync(_windowSystem, w => HasTitle(w, title));

        _policy.Evaluate(window).ShouldBe(EligibilityResult.Excluded(ExclusionReason.TooSmall));
    }

    [Fact]
    public async Task Owned_dialog_is_excluded_while_main_window_stays_eligible()
    {
        var title = UniqueTitle("con-dialogo");
        using var target = TestTargetProcess.Start("--title", title, "--owner-dialog");

        var dialog = await target.WaitForWindowAsync(_windowSystem, w => w.Title.EndsWith("diálogo", StringComparison.Ordinal));
        var main = _windowSystem.EnumerateTopLevel().Single(w => w.ProcessId == target.Process.Id && HasTitle(w, title));

        dialog.HasOwner.ShouldBeTrue();
        _policy.Evaluate(dialog).ShouldBe(EligibilityResult.Excluded(ExclusionReason.HasOwner));
        _policy.Evaluate(main).ShouldBe(EligibilityResult.Eligible);
    }

    [Fact]
    public async Task Closed_window_is_reported_as_gone()
    {
        var title = UniqueTitle("proyecto-beta");
        WindowHandle handle;
        using (var target = TestTargetProcess.Start("--title", title))
        {
            var window = await target.WaitForWindowAsync(_windowSystem, w => HasTitle(w, title));
            handle = window.Handle;

            _windowSystem.IsAlive(handle).ShouldBeTrue();
            _windowSystem.GetInfo(handle).IsSuccess.ShouldBeTrue();
        }

        _windowSystem.IsAlive(handle).ShouldBeFalse();
        var result = _windowSystem.GetInfo(handle);
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("window.notFound");
    }

    [Fact]
    public void Own_process_windows_are_never_eligible()
    {
        foreach (var window in _windowSystem.EnumerateTopLevel().Where(w => w.ProcessId == Environment.ProcessId))
            _policy.Evaluate(window).Reason.ShouldBe(ExclusionReason.OwnProcess);
    }

    private static bool HasTitle(WindowInfo window, string title) => string.Equals(window.Title, title, StringComparison.Ordinal);

    private static string UniqueTitle(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}
