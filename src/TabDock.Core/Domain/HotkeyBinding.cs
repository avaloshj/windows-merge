using System.Globalization;
using System.Text;

namespace TabDock.Core.Domain;

/// <summary>Atajo global. <see cref="VirtualKey"/> es el código de tecla virtual de Windows (p. ej. 0x4D = M).</summary>
public sealed record HotkeyBinding(HotkeyCommand Command, HotkeyModifiers Modifiers, int VirtualKey, bool Enabled = true)
{
    public const int VkTab = 0x09;
    public const int VkPageUp = 0x21;
    public const int VkPageDown = 0x22;

    /// <summary>Texto neutro para logs, p. ej. <c>Ctrl+Alt+Shift+PageDown</c>. La UI usa nombres localizados.</summary>
    public string ToDisplayString()
    {
        var text = new StringBuilder();
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
            text.Append("Ctrl+");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            text.Append("Alt+");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            text.Append("Shift+");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
            text.Append("Win+");

        text.Append(VirtualKey switch
        {
            VkTab => "Tab",
            VkPageUp => "PageUp",
            VkPageDown => "PageDown",
            >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)VirtualKey).ToString(),
            _ => "0x" + VirtualKey.ToString("X2", CultureInfo.InvariantCulture),
        });
        return text.ToString();
    }

    public static IReadOnlyList<HotkeyBinding> Defaults { get; } = CreateDefaults();

    private static HotkeyBinding[] CreateDefaults()
    {
        const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;
        var bindings = new List<HotkeyBinding>
        {
            new(HotkeyCommand.MergeAll, CtrlAlt, 'M'),
            new(HotkeyCommand.UnmergeAll, CtrlAlt, 'U'),
            // Ctrl+Alt+Tab lo reserva Windows (RegisterHotKey falla con 1409): se usa AvPág/RePág, como Ctrl+AvPág en editores.
            new(HotkeyCommand.NextTab, CtrlAlt, VkPageDown),
            new(HotkeyCommand.PreviousTab, CtrlAlt, VkPageUp),
            new(HotkeyCommand.ToggleSidebar, CtrlAlt, 'B'),
            new(HotkeyCommand.QuickSearch, CtrlAlt, 'P'),
        };
        for (var n = 1; n <= 9; n++)
            bindings.Add(new HotkeyBinding(HotkeyCommand.GoToTab1 + (n - 1), CtrlAlt, '0' + n));

        return [.. bindings];
    }
}
