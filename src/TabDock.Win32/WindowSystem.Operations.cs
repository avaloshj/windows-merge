using System.Runtime.InteropServices;
using TabDock.Core;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TabDock.Win32;

/// <summary>Operaciones sobre ventanas de otros procesos: captura, restauración, ocultación y posicionamiento.</summary>
public sealed partial class WindowSystem
{
    /// <summary>Esperas entre reintentos de <c>SetForegroundWindow</c> (fallos transitorios durante animaciones del sistema).</summary>
    private static readonly int[] s_foregroundRetryDelaysMs = [50, 100, 200];

    private const SET_WINDOW_POS_FLAGS PlaceFlags =
        SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER;

    public bool IsHung(WindowHandle handle) => PInvoke.IsHungAppWindow(ToHwnd(handle));

    public bool IsVisible(WindowHandle handle) => PInvoke.IsWindowVisible(ToHwnd(handle));

    public int GetCaptionInset(WindowHandle handle)
    {
        var hwnd = ToHwnd(handle);
        var origin = default(System.Drawing.Point);
        if (!PInvoke.IsWindow(hwnd) || !PInvoke.ClientToScreen(hwnd, ref origin))
            return 0;

        return Math.Max(0, origin.Y - ReadFrameBounds(hwnd).Top);
    }

    public bool Matches(WindowIdentity identity)
    {
        var hwnd = ToHwnd(identity.Handle);
        if (!PInvoke.IsWindow(hwnd))
            return false;

        PInvoke.GetWindowThreadProcessId(hwnd, out var pid);
        if ((int)pid != identity.ProcessId)
            return false;

        if (identity.ProcessStartTime is null)
            return true;

        var start = ReadProcess(pid).StartTime;
        return start is null || start == identity.ProcessStartTime;
    }

    public Result<WindowSnapshot> CaptureSnapshot(WindowHandle handle)
    {
        var hwnd = ToHwnd(handle);
        if (!PInvoke.IsWindow(hwnd))
            return Result.Fail<WindowSnapshot>(Win32Errors.WindowNotFound);

        var placement = NewPlacement();
        if (!PInvoke.GetWindowPlacement(hwnd, ref placement))
            return Result.Fail<WindowSnapshot>(Win32Errors.LastError("window.placementFailed"));

        if (!PInvoke.GetWindowRect(hwnd, out var rect))
            return Result.Fail<WindowSnapshot>(Win32Errors.LastError("window.rectFailed"));

        PInvoke.GetWindowThreadProcessId(hwnd, out var pid);
        var identity = new WindowIdentity(handle, (int)pid, ReadProcess(pid).StartTime);

        return Result.Success(new WindowSnapshot(
            identity,
            ToData(placement),
            ToPixelRect(rect),
            WasVisible: PInvoke.IsWindowVisible(hwnd),
            WasMaximized: PInvoke.IsZoomed(hwnd),
            WasMinimized: PInvoke.IsIconic(hwnd),
            CapturedAt: time.GetUtcNow(),
            ShadowSuppressed: HasOwnChrome(hwnd)));
    }

    public unsafe Result SetFrameShadow(WindowHandle handle, bool visible)
    {
        var hwnd = ToHwnd(handle);
        if (!PInvoke.IsWindow(hwnd))
            return Result.Fail(Win32Errors.WindowNotFound);

        var policy = visible ? DWMNCRENDERINGPOLICY.DWMNCRP_USEWINDOWSTYLE : DWMNCRENDERINGPOLICY.DWMNCRP_DISABLED;
        var hr = PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_NCRENDERING_POLICY, &policy, (uint)sizeof(DWMNCRENDERINGPOLICY));
        return hr.Succeeded ? Result.Success() : Result.Fail(new DomainError("window.shadowFailed", "Error_WindowOperationFailed", hr.Value));
    }

    /// <summary>
    /// Barra de título propia: el área de cliente ocupa toda la ventana (la aplicación responde a <c>WM_NCCALCSIZE</c>
    /// sin dejar marco). Una ventana minimizada o maximizada no permite saberlo con seguridad: se deja como está.
    /// </summary>
    private static bool HasOwnChrome(HWND hwnd)
    {
        if (PInvoke.IsIconic(hwnd) || PInvoke.IsZoomed(hwnd))
            return false;

        return PInvoke.GetWindowRect(hwnd, out var window) && PInvoke.GetClientRect(hwnd, out var client)
            && client.right - client.left == window.right - window.left
            && client.bottom - client.top == window.bottom - window.top;
    }

    public Result Restore(WindowSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!Matches(snapshot.Identity))
            return Result.Fail(Win32Errors.IdentityMismatch);

        var hwnd = ToHwnd(snapshot.Identity.Handle);
        var placement = ToNative(snapshot.Placement);
        placement.showCmd = !snapshot.WasVisible ? SHOW_WINDOW_CMD.SW_HIDE
            : snapshot.WasMinimized ? SHOW_WINDOW_CMD.SW_SHOWMINNOACTIVE
            : snapshot.WasMaximized ? SHOW_WINDOW_CMD.SW_SHOWMAXIMIZED
            : SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE;

        if (!PInvoke.SetWindowPlacement(hwnd, in placement))
            return Result.Fail(Win32Errors.LastError("window.restoreFailed"));

        // Acoplada con Snap: la colocación "normal" no es el rectángulo real; se devuelve a donde estaba.
        var wasNormal = snapshot.WasVisible && !snapshot.WasMaximized && !snapshot.WasMinimized;
        if (wasNormal && PInvoke.GetWindowRect(hwnd, out var current) && ToPixelRect(current) != snapshot.ActualBounds)
        {
            var bounds = snapshot.ActualBounds;
            if (!PInvoke.SetWindowPos(hwnd, default, bounds.Left, bounds.Top, bounds.Width, bounds.Height, PlaceFlags))
                return Result.Fail(Win32Errors.LastError("window.restoreFailed"));
        }

        if (snapshot.ShadowSuppressed)
            _ = SetFrameShadow(snapshot.Identity.Handle, visible: true);

        // La ventana activa del grupo tenía su botón quitado de la barra de tareas: se devuelve.
        if (snapshot.WasVisible)
            _ = SetTaskbarButton(snapshot.Identity.Handle, visible: true);

        return Result.Success();
    }

    public Result Show(WindowHandle handle) => SetVisibility(handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);

    public Result Hide(WindowHandle handle) => SetVisibility(handle, SHOW_WINDOW_CMD.SW_HIDE);

    private const int MaxZOrderSteps = 10_000;

    public Result PlaceInSlot(WindowHandle handle, PixelRect slot)
    {
        var hwnd = ToHwnd(handle);
        if (!PInvoke.IsWindow(hwnd))
            return Result.Fail(Win32Errors.WindowNotFound);

        if (PInvoke.IsZoomed(hwnd) || PInvoke.IsIconic(hwnd))
            RestoreToNormalWithoutActivating(hwnd);

        var hung = PInvoke.IsHungAppWindow(hwnd);
        var flags = hung ? PlaceFlags | SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS : PlaceFlags;

        // Segunda pasada: si la ventana cambió de monitor y de DPI, sus bordes invisibles cambian de grosor.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var border = InvisibleBorders(hwnd);
            var ok = PInvoke.SetWindowPos(
                hwnd,
                default,
                slot.Left - border.Left,
                slot.Top - border.Top,
                slot.Width + border.Left + border.Right,
                slot.Height + border.Top + border.Bottom,
                flags);
            if (!ok)
                return Result.Fail(Win32Errors.LastError("window.placeFailed"));

            if (hung || ReadFrameBounds(hwnd) == slot)
                break;
        }

        return Result.Success();
    }

    public bool IsAbove(WindowHandle upper, WindowHandle lower)
    {
        // Recorre hacia abajo desde 'upper'; el límite evita un bucle si el orden cambia mientras se recorre.
        var hwnd = ToHwnd(upper);
        for (var i = 0; i < MaxZOrderSteps; i++)
        {
            hwnd = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_HWNDNEXT);
            if (hwnd.IsNull)
                return false;

            if (hwnd == ToHwnd(lower))
                return true;
        }

        return false;
    }

    /// <summary>
    /// <c>SetWindowPos</c> coloca una ventana <em>después</em> de otra: para quedar justo encima de
    /// <paramref name="below"/> se coloca después de la que tiene encima. Si esa es "siempre visible" (o no hay ninguna),
    /// se usa <c>HWND_TOP</c>: colocarse tras una ventana "siempre visible" convertiría el marco en una.
    /// </summary>
    public Result PlaceAbove(WindowHandle window, WindowHandle below)
    {
        const SET_WINDOW_POS_FLAGS Flags = SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
            | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER;

        var hwnd = ToHwnd(window);
        var previous = PInvoke.GetWindow(ToHwnd(below), GET_WINDOW_CMD.GW_HWNDPREV);
        if (previous == hwnd)
            return Result.Success();

        var topmost = !previous.IsNull
            && ((WINDOW_EX_STYLE)PInvoke.GetWindowLong(previous, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE)).HasFlag(WINDOW_EX_STYLE.WS_EX_TOPMOST);
        var insertAfter = previous.IsNull || topmost ? HWND.Null : previous;   // HWND_TOP = 0

        return PInvoke.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, Flags)
            ? Result.Success()
            : Result.Fail(Win32Errors.LastError("window.zOrderFailed"));
    }

    public bool IsForeground(WindowHandle handle) => PInvoke.GetForegroundWindow() == ToHwnd(handle);

    /// <summary>
    /// Solo tiene éxito si TabDock tiene derecho a cambiar el primer plano (p. ej. tras un clic en su barra lateral
    /// o un atajo de teclado). No se usan trucos como <c>AttachThreadInput</c> ni pulsaciones simuladas.
    /// </summary>
    public Result BringToForeground(WindowHandle handle)
    {
        var hwnd = ToHwnd(handle);
        for (var attempt = 0; ; attempt++)
        {
            if (PInvoke.SetForegroundWindow(hwnd) || PInvoke.GetForegroundWindow() == hwnd)
                return Result.Success();

            if (attempt >= s_foregroundRetryDelaysMs.Length)
                return Result.Fail(Win32Errors.ForegroundDenied);

            Thread.Sleep(s_foregroundRetryDelaysMs[attempt]);
        }
    }

    public Result RequestClose(WindowHandle handle) =>
        PInvoke.PostMessage(ToHwnd(handle), PInvoke.WM_CLOSE, default, default)
            ? Result.Success()
            : Result.Fail(Win32Errors.LastError("window.closeFailed"));

    /// <summary>
    /// <c>ShowWindow</c> devuelve la visibilidad anterior, no un error. Para ventanas que no responden se usa la
    /// variante asíncrona, que no bloquea a TabDock.
    /// </summary>
    private static Result SetVisibility(WindowHandle handle, SHOW_WINDOW_CMD command)
    {
        var hwnd = ToHwnd(handle);
        if (!PInvoke.IsWindow(hwnd))
            return Result.Fail(Win32Errors.WindowNotFound);

        if (PInvoke.IsHungAppWindow(hwnd))
            _ = PInvoke.ShowWindowAsync(hwnd, command);
        else
            _ = PInvoke.ShowWindow(hwnd, command);

        return Result.Success();
    }

    private static void RestoreToNormalWithoutActivating(HWND hwnd)
    {
        var placement = NewPlacement();
        if (PInvoke.GetWindowPlacement(hwnd, ref placement))
        {
            placement.showCmd = SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE;
            _ = PInvoke.SetWindowPlacement(hwnd, in placement);
        }

        if (PInvoke.IsZoomed(hwnd) || PInvoke.IsIconic(hwnd))
            _ = PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
    }

    /// <summary>Diferencia entre <c>GetWindowRect</c> y el área visible (<c>DWMWA_EXTENDED_FRAME_BOUNDS</c>).</summary>
    private static PixelRect InvisibleBorders(HWND hwnd)
    {
        if (!PInvoke.GetWindowRect(hwnd, out var rect))
            return default;

        var visible = ReadFrameBounds(hwnd);
        if (visible == default)
            return default;

        return new PixelRect(
            Math.Max(0, visible.Left - rect.left),
            Math.Max(0, visible.Top - rect.top),
            Math.Max(0, rect.right - visible.Right),
            Math.Max(0, rect.bottom - visible.Bottom));
    }

    private static WINDOWPLACEMENT NewPlacement() => new() { length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>() };

    private static PixelRect ToPixelRect(RECT rect) => new(rect.left, rect.top, rect.right, rect.bottom);

    private static WindowPlacementData ToData(WINDOWPLACEMENT placement) => new(
        (int)placement.flags,
        (int)placement.showCmd,
        placement.ptMinPosition.X,
        placement.ptMinPosition.Y,
        placement.ptMaxPosition.X,
        placement.ptMaxPosition.Y,
        ToPixelRect(placement.rcNormalPosition));

    private static WINDOWPLACEMENT ToNative(WindowPlacementData data)
    {
        var placement = NewPlacement();
        placement.flags = (WINDOWPLACEMENT_FLAGS)data.Flags;
        placement.showCmd = (SHOW_WINDOW_CMD)data.ShowCommand;
        placement.ptMinPosition = new System.Drawing.Point(data.MinX, data.MinY);
        placement.ptMaxPosition = new System.Drawing.Point(data.MaxX, data.MaxY);
        placement.rcNormalPosition = new RECT { left = data.Normal.Left, top = data.Normal.Top, right = data.Normal.Right, bottom = data.Normal.Bottom };
        return placement;
    }
}
