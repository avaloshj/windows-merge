using System.Globalization;
using System.Text;
using System.Windows.Input;
using TabDock.App.Localization;
using TabDock.Core.Domain;

namespace TabDock.App.Services;

/// <summary>Nombre localizado de un atajo para mostrarlo al usuario (p. ej. <c>Ctrl+Alt+AvPág</c>).</summary>
internal static class HotkeyText
{
    private const int VkF1 = 0x70;
    private const int VkF24 = 0x87;

    public static string Format(HotkeyBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var text = new StringBuilder();
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Control))
            text.Append(TextResources.Get("Key_Ctrl")).Append('+');
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Alt))
            text.Append(TextResources.Get("Key_Alt")).Append('+');
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Shift))
            text.Append(TextResources.Get("Key_Shift")).Append('+');
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Windows))
            text.Append(TextResources.Get("Key_Win")).Append('+');

        text.Append(binding.VirtualKey switch
        {
            HotkeyBinding.VkTab => TextResources.Get("Key_Tab"),
            HotkeyBinding.VkPageUp => TextResources.Get("Key_PageUp"),
            HotkeyBinding.VkPageDown => TextResources.Get("Key_PageDown"),
            >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)binding.VirtualKey).ToString(),
            >= VkF1 and <= VkF24 => "F" + (binding.VirtualKey - VkF1 + 1).ToString(CultureInfo.InvariantCulture),
            _ => KeyName(binding.VirtualKey),
        });
        return text.ToString();
    }

    /// <summary>Nombre de WPF para el resto de teclas (flechas, Inicio, teclado numérico…); si no tiene, el código.</summary>
    private static string KeyName(int virtualKey)
    {
        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return key == Key.None ? "0x" + virtualKey.ToString("X2", CultureInfo.InvariantCulture) : key.ToString();
    }
}
