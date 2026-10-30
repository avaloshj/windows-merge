using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using TabDock.Core.Domain;
using TabDock.Win32.Input;

namespace TabDock.App.Services;

/// <summary>
/// Registra los atajos globales en una ventana de solo mensajes (<c>HWND_MESSAGE</c>) y publica el comando de cada
/// <c>WM_HOTKEY</c>. Los atajos que otro programa ya usa se devuelven como conflictos para avisar al usuario.
/// </summary>
public sealed partial class HotkeyService(ILogger<HotkeyService> logger) : IHotkeyControl, IDisposable
{
    private static readonly nint s_messageOnlyParent = -3;   // HWND_MESSAGE

    private readonly Dictionary<int, HotkeyBinding> _registered = [];
    private IReadOnlyList<HotkeyBinding> _bindings = [];
    private HwndSource? _source;
    private bool _suspended;

    public event EventHandler<HotkeyCommandEventArgs>? CommandInvoked;

    /// <summary>Atajos que no se pudieron registrar en el último <see cref="Register"/>.</summary>
    public IReadOnlyList<HotkeyBinding> Conflicts { get; private set; } = [];

    /// <summary>Registra los atajos activos (sustituye los anteriores). Devuelve los que no se pudieron registrar.</summary>
    public IReadOnlyList<HotkeyBinding> Register(IEnumerable<HotkeyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        _bindings = [.. bindings];
        if (_suspended)
            return Conflicts;

        _source ??= CreateSource();
        UnregisterAll();

        var conflicts = new List<HotkeyBinding>();
        var id = 1;
        foreach (var binding in _bindings.Where(b => b.Enabled))
        {
            var result = GlobalHotkeys.Register(_source.Handle, id, binding.Modifiers, binding.VirtualKey);
            if (result.IsSuccess)
            {
                _registered[id] = binding;
            }
            else
            {
                conflicts.Add(binding);
                LogConflict(binding.Command, binding.ToDisplayString(), result.Error.Win32Error);
            }

            id++;
        }

        LogRegistered(_registered.Count, conflicts.Count);
        Conflicts = conflicts;
        return conflicts;
    }

    /// <summary>
    /// Libera los atajos mientras el usuario escribe uno nuevo en Ajustes: un atajo registrado no llega nunca a la
    /// ventana que tiene el foco, así que no se podría volver a asignar.
    /// </summary>
    public void SuspendAll()
    {
        _suspended = true;
        UnregisterAll();
    }

    public void ResumeAll()
    {
        if (!_suspended)
            return;

        _suspended = false;
        Register(_bindings);
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.Dispose();
        _source = null;
    }

    private HwndSource CreateSource()
    {
        var source = new HwndSource(new HwndSourceParameters("TabDock.Hotkeys") { ParentWindow = s_messageOnlyParent, WindowStyle = 0 });
        source.AddHook(WndProc);
        return source;
    }

    private void UnregisterAll()
    {
        if (_source is null)
            return;

        foreach (var id in _registered.Keys)
            GlobalHotkeys.Unregister(_source.Handle, id);

        _registered.Clear();
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == GlobalHotkeys.HotkeyMessage && _registered.TryGetValue((int)wParam, out var binding))
        {
            handled = true;
            CommandInvoked?.Invoke(this, new HotkeyCommandEventArgs(binding.Command));
        }

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Atajos globales: {Registered} registrados, {Conflicts} en conflicto")]
    private partial void LogRegistered(int registered, int conflicts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Atajo {Command} ({Keys}) en uso por otro programa (Win32 {Win32Error})")]
    private partial void LogConflict(HotkeyCommand command, string keys, int? win32Error);
}
