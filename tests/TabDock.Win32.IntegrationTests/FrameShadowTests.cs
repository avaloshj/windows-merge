using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace TabDock.Win32.IntegrationTests;

/// <summary>Sombra de DWM en ventanas de otro proceso (TestTarget): solo se quita a las que tienen barra de título propia.</summary>
[Trait("Category", "Integration")]
public sealed partial class FrameShadowTests
{
    private const int DwmwaNcRenderingEnabled = 1;

    private readonly WindowSystem _windows = new(NullLogger<WindowSystem>.Instance, TimeProvider.System);

    [Fact]
    public async Task A_window_with_a_native_frame_keeps_its_shadow()
    {
        using var target = TestTargetProcess.Start("--title", Unique("proyecto-alfa"));
        var window = await target.WaitForWindowAsync(_windows, _ => true);

        _windows.CaptureSnapshot(window.Handle).Value!.ShadowSuppressed.ShouldBeFalse();
    }

    /// <summary>
    /// Como VS Code: barra de título propia pero bordes de redimensionado nativos (invisibles). Sin el dibujo de DWM,
    /// Windows pintaría un borde clásico en esos bordes (verificado en la Fase 7), así que conserva la sombra.
    /// </summary>
    [Fact]
    public async Task A_window_with_native_resize_edges_keeps_its_shadow()
    {
        using var target = TestTargetProcess.Start("--title", Unique("demo-api"), "--custom-chrome");
        var window = await target.WaitForWindowAsync(_windows, _ => true);

        _windows.CaptureSnapshot(window.Handle).Value!.ShadowSuppressed.ShouldBeFalse();
    }

    [Fact]
    public async Task A_window_without_any_native_frame_loses_the_shadow_and_gets_it_back_on_restore()
    {
        using var target = TestTargetProcess.Start("--title", Unique("proyecto-beta"), "--frameless");
        var window = await target.WaitForWindowAsync(_windows, _ => true);
        var snapshot = _windows.CaptureSnapshot(window.Handle).Value!;
        snapshot.ShadowSuppressed.ShouldBeTrue();
        IsNonClientRenderingEnabled(window.Handle.Value).ShouldBeTrue();

        _windows.SetFrameShadow(window.Handle, visible: false).IsSuccess.ShouldBeTrue();
        IsNonClientRenderingEnabled(window.Handle.Value).ShouldBeFalse();

        _windows.Restore(snapshot).IsSuccess.ShouldBeTrue();
        IsNonClientRenderingEnabled(window.Handle.Value).ShouldBeTrue();
    }

    private static string Unique(string name) => $"{name}-{Guid.NewGuid():N} - TestTarget";

    private static bool IsNonClientRenderingEnabled(nint hwnd) =>
        DwmGetWindowAttribute(hwnd, DwmwaNcRenderingEnabled, out var enabled, sizeof(int)) == 0 && enabled != 0;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
}
