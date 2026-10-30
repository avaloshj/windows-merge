using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using TabDock.Core;
using TabDock.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.WindowsAndMessaging;
using ComFileTime = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace TabDock.Win32;

/// <summary>Implementación Win32 de <see cref="IWindowSystem"/>.</summary>
public sealed partial class WindowSystem(ILogger<WindowSystem> logger, TimeProvider time) : IWindowSystem
{
    private const int MaxTitleLength = 1024;
    private const int MaxClassNameLength = 256;   // Límite documentado de lpszClassName en WNDCLASSEX.
    private const int MaxPathLength = 32_767;

    public IReadOnlyList<WindowInfo> EnumerateTopLevel()
    {
        var handles = new List<HWND>(capacity: 256);
        if (!PInvoke.EnumWindows((hwnd, _) => { handles.Add(hwnd); return true; }, default))
        {
            var error = Marshal.GetLastPInvokeError();
            LogWin32Failure(nameof(PInvoke.EnumWindows), error);
        }

        // Muchas ventanas comparten proceso: se consulta cada proceso una sola vez por enumeración.
        var processes = new Dictionary<uint, ProcessDetails>();
        var windows = new List<WindowInfo>(handles.Count);
        foreach (var hwnd in handles)
        {
            if (TryRead(hwnd, processes) is { } info)
                windows.Add(info);
        }

        return windows;
    }

    public Result<WindowInfo> GetInfo(WindowHandle handle)
    {
        var info = TryRead(ToHwnd(handle), new Dictionary<uint, ProcessDetails>());
        return info is null
            ? Result.Fail<WindowInfo>(Win32Errors.WindowNotFound)
            : Result.Success(info);
    }

    public bool IsAlive(WindowHandle handle) => PInvoke.IsWindow(ToHwnd(handle));

    internal static HWND ToHwnd(WindowHandle handle) => (HWND)handle.Value;

    private static unsafe WindowHandle ToHandle(HWND hwnd) => new((nint)hwnd.Value);

    private WindowInfo? TryRead(HWND hwnd, Dictionary<uint, ProcessDetails> processes)
    {
        // La ventana puede destruirse durante la lectura; en ese caso se descarta.
        if (!PInvoke.IsWindow(hwnd))
            return null;

        PInvoke.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return null;

        if (!processes.TryGetValue(pid, out var process))
        {
            process = ReadProcess(pid);
            processes[pid] = process;
        }

        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        return new WindowInfo
        {
            Handle = ToHandle(hwnd),
            ProcessId = (int)pid,
            ProcessStartTime = process.StartTime,
            ExecutablePath = process.ExecutablePath,
            ClassName = ReadClassName(hwnd),
            Title = ReadTitle(hwnd),
            ExtendedFrameBounds = ReadFrameBounds(hwnd),
            IsVisible = PInvoke.IsWindowVisible(hwnd),
            IsCloaked = ReadIsCloaked(hwnd),
            HasOwner = !PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER).IsNull,
            IsToolWindow = exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW),
            IsProcessAccessible = process.IsAccessible,
            IsElevatedProcess = process.IsElevated,
            ShowState = PInvoke.IsIconic(hwnd) ? WindowShowState.Minimized
                : PInvoke.IsZoomed(hwnd) ? WindowShowState.Maximized
                : WindowShowState.Normal,
        };
    }

    /// <summary>Lee el título de la ventana (máximo 1024 caracteres).</summary>
    /// <remarks>
    /// Para ventanas de otro proceso <c>GetWindowText</c> no envía <c>WM_GETTEXT</c>: lee el texto interno,
    /// por lo que no se bloquea aunque la ventana no responda.
    /// </remarks>
    private static string ReadTitle(HWND hwnd)
    {
        var length = Math.Min(PInvoke.GetWindowTextLength(hwnd), MaxTitleLength);
        if (length <= 0)
            return string.Empty;

        Span<char> buffer = stackalloc char[length + 1];
        var copied = PInvoke.GetWindowText(hwnd, buffer);
        return copied > 0 ? new string(buffer[..copied]) : string.Empty;
    }

    private static string ReadClassName(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[MaxClassNameLength + 1];
        var copied = PInvoke.GetClassName(hwnd, buffer);
        return copied > 0 ? new string(buffer[..copied]) : string.Empty;
    }

    private static unsafe PixelRect ReadFrameBounds(HWND hwnd)
    {
        RECT rect;
        var hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, (uint)sizeof(RECT));
        if (hr.Failed && !PInvoke.GetWindowRect(hwnd, out rect))
            return default;

        return new PixelRect(rect.left, rect.top, rect.right, rect.bottom);
    }

    private static unsafe bool ReadIsCloaked(HWND hwnd)
    {
        uint cloaked = 0;
        var hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
        return hr.Succeeded && cloaked != 0;
    }

    private ProcessDetails ReadProcess(uint pid)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            LogProcessInaccessible(pid, error);
            return ProcessDetails.Inaccessible;
        }

        return new ProcessDetails(
            ExecutablePath: ReadExecutablePath(process),
            StartTime: ReadStartTime(process),
            IsAccessible: true,
            IsElevated: ReadIsElevated(process));
    }

    private static string ReadExecutablePath(SafeFileHandle process)
    {
        var buffer = new char[MaxPathLength];
        var size = (uint)buffer.Length;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref size)
            ? new string(buffer, 0, (int)size)
            : string.Empty;
    }

    private static DateTimeOffset? ReadStartTime(SafeFileHandle process)
    {
        if (!PInvoke.GetProcessTimes(process, out var creation, out _, out _, out _))
            return null;

        return new DateTimeOffset(DateTime.FromFileTimeUtc(ToInt64(creation)));
    }

    private static long ToInt64(ComFileTime time) => ((long)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;

    /// <summary>
    /// Un proceso elevado no deja abrir su token desde un proceso estándar: el fallo se trata como elevado,
    /// porque en ambos casos UIPI impediría gestionar sus ventanas.
    /// </summary>
    private static unsafe bool ReadIsElevated(SafeFileHandle process)
    {
        if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
            return true;

        using (token)
        {
            TOKEN_ELEVATION elevation;
            return !PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenElevation, &elevation, (uint)sizeof(TOKEN_ELEVATION), out _)
                || elevation.TokenIsElevated != 0;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fallo Win32 en {Function}: error {Win32Error}")]
    private partial void LogWin32Failure(string function, int win32Error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Proceso {ProcessId} no accesible: error {Win32Error}")]
    private partial void LogProcessInaccessible(uint processId, int win32Error);

    private readonly record struct ProcessDetails(string ExecutablePath, DateTimeOffset? StartTime, bool IsAccessible, bool IsElevated)
    {
        public static ProcessDetails Inaccessible { get; } = new(string.Empty, null, IsAccessible: false, IsElevated: false);
    }
}
