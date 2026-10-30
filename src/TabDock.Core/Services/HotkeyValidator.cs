using TabDock.Core.Domain;

namespace TabDock.Core.Services;

/// <summary>
/// Reglas para un atajo nuevo antes de guardarlo. Que otro programa ya lo use solo se sabe al registrarlo
/// (<c>RegisterHotKey</c>); esto evita lo que se puede saber antes.
/// </summary>
public static class HotkeyValidator
{
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLeftWin = 0x5B;
    private const int VkRightWin = 0x5C;
    private const int VkLeftShift = 0xA0;
    private const int VkRightMenu = 0xA5;

    /// <summary>Comprueba un atajo propuesto frente a los vigentes.</summary>
    /// <param name="candidate">Atajo propuesto.</param>
    /// <param name="current">Atajos vigentes (se ignora el del mismo comando).</param>
    public static HotkeyValidation Validate(HotkeyBinding candidate, IEnumerable<HotkeyBinding> current)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(current);

        if (IsModifierKey(candidate.VirtualKey))
            return HotkeyValidation.Invalid("Hotkey_Error_ModifierOnly");

        // Sin Ctrl, Alt ni Win el atajo robaría una tecla normal (o Mayús+letra) en todos los programas.
        if ((candidate.Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows)) == HotkeyModifiers.None)
            return HotkeyValidation.Invalid("Hotkey_Error_NeedsModifier");

        if (IsReservedBySystem(candidate))
            return HotkeyValidation.Invalid("Hotkey_Error_Reserved");

        var clash = current.FirstOrDefault(h => h.Command != candidate.Command && h.Enabled
            && h.Modifiers == candidate.Modifiers && h.VirtualKey == candidate.VirtualKey);

        return clash is null ? HotkeyValidation.Valid : HotkeyValidation.Invalid("Hotkey_Error_Duplicate", clash.Command);
    }

    private static bool IsModifierKey(int virtualKey) =>
        virtualKey is VkShift or VkControl or VkMenu or VkLeftWin or VkRightWin or (>= VkLeftShift and <= VkRightMenu);

    /// <summary>Combinaciones que <c>RegisterHotKey</c> rechaza siempre (error 1409), verificadas en la Fase 4.</summary>
    private static bool IsReservedBySystem(HotkeyBinding binding) =>
        binding.VirtualKey == HotkeyBinding.VkTab
        && binding.Modifiers.HasFlag(HotkeyModifiers.Control)
        && binding.Modifiers.HasFlag(HotkeyModifiers.Alt);
}
