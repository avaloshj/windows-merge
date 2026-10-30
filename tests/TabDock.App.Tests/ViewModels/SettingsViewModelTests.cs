using TabDock.App.Tests.Fakes;
using TabDock.App.ViewModels;
using TabDock.Core;
using TabDock.Core.Domain;
using TabDock.Core.Services;

namespace TabDock.App.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    private readonly InMemorySettingsStore _store = new();
    private readonly FakeStartupRegistration _startup = new();
    private readonly FakeHotkeyControl _hotkeys = new();
    private readonly List<RunningProgram> _running = [new("Bloc de notas", "notepad.exe"), new("Visual Studio Code", "Code.exe")];
    private readonly List<string> _exports = [];
    private readonly SettingsService _settings;

    public SettingsViewModelTests()
    {
        _settings = new SettingsService(_store, TimeProvider.System);
    }

    [Fact]
    public void Loads_current_values_without_writing()
    {
        _startup.Enabled = true;

        var vm = Create();

        vm.StartWithWindows.ShouldBeTrue();
        vm.Theme.ShouldBe(ThemePreference.System);
        vm.Rules.Count.ShouldBe(4);
        vm.Hotkeys.Count.ShouldBe(HotkeyBinding.Defaults.Count);
        _store.Saved.User.StartWithWindows.ShouldBeFalse();
    }

    [Fact]
    public void Appearance_and_advanced_changes_are_saved_at_once()
    {
        var vm = Create();

        vm.Theme = ThemePreference.Dark;
        vm.UseMica = false;
        vm.Density = Density.Compact;
        vm.HealthCheckSeconds = 5;

        var user = _store.Saved.User;
        user.Theme.ShouldBe(ThemePreference.Dark);
        user.UseMica.ShouldBeFalse();
        user.Density.ShouldBe(Density.Compact);
        user.HealthCheckSeconds.ShouldBe(5);
    }

    [Fact]
    public void Low_memory_rendering_is_saved_and_asks_for_a_restart()
    {
        var vm = Create();

        vm.RenderingNeedsRestart.ShouldBeFalse();
        vm.LowMemoryRendering = true;

        vm.RenderingNeedsRestart.ShouldBeTrue();
        _store.Saved.User.LowMemoryRendering.ShouldBeTrue();
    }

    [Fact]
    public void Changing_language_is_saved_and_asks_for_a_restart()
    {
        var vm = Create();

        vm.LanguageNeedsRestart.ShouldBeFalse();
        vm.Language = "en";

        vm.LanguageNeedsRestart.ShouldBeTrue();
        _store.Saved.User.Language.ShouldBe("en");
    }

    [Fact]
    public void Start_with_windows_writes_the_registry_and_the_settings()
    {
        var vm = Create();

        vm.StartWithWindows = true;

        _startup.Enabled.ShouldBeTrue();
        _store.Saved.User.StartWithWindows.ShouldBeTrue();
        vm.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void A_registry_failure_reverts_the_checkbox_and_shows_an_error()
    {
        _startup.Fails = true;
        var vm = Create();

        vm.StartWithWindows = true;

        vm.StartWithWindows.ShouldBeFalse();
        vm.ErrorMessage.ShouldNotBeNullOrEmpty();
        _store.Saved.User.StartWithWindows.ShouldBeFalse();
    }

    [Fact]
    public void Hide_mode_cannot_be_set_to_an_unimplemented_mode()
    {
        var vm = Create();

        vm.HideModes.Single(o => o.Value == HideMode.TaskbarOnly).IsEnabled.ShouldBeFalse();
        vm.HideMode = HideMode.TaskbarOnly;

        vm.HideMode.ShouldBe(HideMode.Hide);
    }

    [Fact]
    public void Only_running_programs_without_a_rule_can_be_added()
    {
        var vm = Create();

        vm.RefreshPrograms();
        vm.AvailablePrograms.Select(p => p.ExecutableName).ShouldBe(["notepad.exe"]);

        vm.AddProgramCommand.Execute(null);

        _store.Saved.Rules.ShouldContain(r => r.ExecutableName == "notepad.exe" && r.Enabled && r.TitleParser == TitleParserKind.Generic);
        vm.Rules.Count.ShouldBe(5);
        vm.AvailablePrograms.ShouldBeEmpty();
        vm.AddProgramCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void Rule_rows_save_their_switches_and_can_be_removed()
    {
        var vm = Create();
        var cursor = vm.Rules.Single(r => r.ExecutableName == "Cursor.exe");

        cursor.Enabled = true;
        cursor.MergeOnStartup = true;
        _store.Saved.Rules.Single(r => r.ExecutableName == "Cursor.exe").ShouldSatisfyAllConditions(
            r => r.Enabled.ShouldBeTrue(),
            r => r.MergeOnStartup.ShouldBeTrue());

        vm.RemoveRule(cursor);
        _store.Saved.Rules.ShouldNotContain(r => r.ExecutableName == "Cursor.exe");
        vm.Rules.Count.ShouldBe(3);
    }

    [Fact]
    public void Capturing_a_hotkey_suspends_the_global_ones_until_it_ends()
    {
        var vm = Create();
        var row = vm.Hotkeys[0];

        vm.BeginCapture(row);
        _hotkeys.IsSuspended.ShouldBeTrue();
        row.IsCapturing.ShouldBeTrue();

        vm.EndCapture();
        _hotkeys.IsSuspended.ShouldBeFalse();
        row.IsCapturing.ShouldBeFalse();
    }

    [Fact]
    public void A_valid_combination_is_saved_and_shown()
    {
        var vm = Create();
        var merge = vm.Hotkeys.Single(h => h.Command == HotkeyCommand.MergeAll);

        vm.AssignHotkey(merge, CtrlAlt | HotkeyModifiers.Shift, 'G').ShouldBeTrue();

        merge.Message.ShouldBeNull();
        merge.KeysText.ShouldEndWith("+G");
        _store.Saved.Hotkeys.Single(h => h.Command == HotkeyCommand.MergeAll).VirtualKey.ShouldBe('G');
    }

    [Fact]
    public void A_duplicate_combination_is_rejected_naming_the_other_command()
    {
        var vm = Create();
        var search = vm.Hotkeys.Single(h => h.Command == HotkeyCommand.QuickSearch);

        vm.AssignHotkey(search, CtrlAlt, 'M').ShouldBeFalse();

        search.HasMessage.ShouldBeTrue();
        search.Message!.ShouldContain(HotkeyRowViewModel.LabelFor(HotkeyCommand.MergeAll));
        _store.Saved.Hotkeys.Single(h => h.Command == HotkeyCommand.QuickSearch).VirtualKey.ShouldBe('P');
    }

    [Fact]
    public void Hotkeys_used_by_another_program_are_flagged()
    {
        _hotkeys.Conflicts = [HotkeyBinding.Defaults.Single(h => h.Command == HotkeyCommand.ToggleSidebar)];

        var vm = Create();

        vm.Hotkeys.Single(h => h.Command == HotkeyCommand.ToggleSidebar).HasMessage.ShouldBeTrue();
        vm.Hotkeys.Single(h => h.Command == HotkeyCommand.MergeAll).HasMessage.ShouldBeFalse();
    }

    [Fact]
    public void A_hotkey_switch_disables_the_command()
    {
        var vm = Create();
        var sidebar = vm.Hotkeys.Single(h => h.Command == HotkeyCommand.ToggleSidebar);

        sidebar.IsActive = false;

        _store.Saved.Hotkeys.Single(h => h.Command == HotkeyCommand.ToggleSidebar).Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Exporting_reports_success_or_the_error()
    {
        var vm = Create();

        vm.ExportDiagnostics(@"C:\temp\tabdock-diagnostico.zip").ShouldBeTrue();
        vm.DiagnosticsStatus!.ShouldContain("tabdock-diagnostico.zip");

        vm.ExportDiagnostics(@"C:\fallo\diag.zip").ShouldBeFalse();
        vm.DiagnosticsStatus!.ShouldNotContain("diag.zip");
        _exports.Count.ShouldBe(2);
    }

    private SettingsViewModel Create() => new(_settings, _startup, _hotkeys, () => _running, Export);

    private Result Export(string path)
    {
        _exports.Add(path);
        return path.Contains("fallo", StringComparison.Ordinal)
            ? Result.Fail(new DomainError("diagnostics.export", "Error_DiagnosticsExport"))
            : Result.Success();
    }
}
