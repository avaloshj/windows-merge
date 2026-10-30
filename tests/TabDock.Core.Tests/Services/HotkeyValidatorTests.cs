using TabDock.Core.Domain;
using TabDock.Core.Services;

namespace TabDock.Core.Tests.Services;

public sealed class HotkeyValidatorTests
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    [Fact]
    public void A_free_combination_with_ctrl_alt_is_valid()
    {
        var result = HotkeyValidator.Validate(new HotkeyBinding(HotkeyCommand.MergeAll, CtrlAlt, 'G'), HotkeyBinding.Defaults);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Keeping_the_same_combination_for_the_same_command_is_valid()
    {
        var result = HotkeyValidator.Validate(new HotkeyBinding(HotkeyCommand.MergeAll, CtrlAlt, 'M'), HotkeyBinding.Defaults);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, 'G')]
    [InlineData(HotkeyModifiers.Shift, 'G')]
    public void Without_ctrl_alt_or_win_it_is_rejected(HotkeyModifiers modifiers, int key)
    {
        HotkeyValidator.Validate(new HotkeyBinding(HotkeyCommand.MergeAll, modifiers, key), []).ErrorKey.ShouldBe("Hotkey_Error_NeedsModifier");
    }

    [Theory]
    [InlineData(0x11)]   // VK_CONTROL
    [InlineData(0xA4)]   // VK_LMENU
    [InlineData(0x5B)]   // VK_LWIN
    public void A_modifier_alone_is_rejected(int key)
    {
        HotkeyValidator.Validate(new HotkeyBinding(HotkeyCommand.MergeAll, CtrlAlt, key), []).ErrorKey.ShouldBe("Hotkey_Error_ModifierOnly");
    }

    [Fact]
    public void Ctrl_alt_tab_is_reserved_by_windows()
    {
        var result = HotkeyValidator.Validate(new HotkeyBinding(HotkeyCommand.NextTab, CtrlAlt | HotkeyModifiers.Shift, HotkeyBinding.VkTab), []);

        result.ErrorKey.ShouldBe("Hotkey_Error_Reserved");
    }

    [Fact]
    public void A_combination_used_by_another_active_command_is_rejected_naming_it()
    {
        var result = HotkeyValidator.Validate(new HotkeyBinding(HotkeyCommand.QuickSearch, CtrlAlt, 'M'), HotkeyBinding.Defaults);

        result.ErrorKey.ShouldBe("Hotkey_Error_Duplicate");
        result.ConflictsWith.ShouldBe(HotkeyCommand.MergeAll);
    }

    [Fact]
    public void A_combination_of_a_disabled_command_can_be_reused()
    {
        var current = HotkeyBinding.Defaults.Select(h => h.Command == HotkeyCommand.MergeAll ? h with { Enabled = false } : h).ToList();

        HotkeyValidator.Validate(new HotkeyBinding(HotkeyCommand.QuickSearch, CtrlAlt, 'M'), current).IsValid.ShouldBeTrue();
    }
}
