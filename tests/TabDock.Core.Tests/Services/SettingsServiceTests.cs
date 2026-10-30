using Microsoft.Extensions.Time.Testing;
using TabDock.Core.Domain;
using TabDock.Core.Services;
using TabDock.Core.Tests.Fakes;

namespace TabDock.Core.Tests.Services;

public sealed class SettingsServiceTests
{
    private readonly FakeSettingsStore _store = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));
    private readonly SettingsService _service;
    private readonly List<SettingsChangedEventArgs> _changes = [];

    public SettingsServiceTests()
    {
        _service = new SettingsService(_store, _time);
        _service.Changed += (_, e) => _changes.Add(e);
    }

    [Fact]
    public void A_change_is_saved_at_once_and_notified_with_what_changed()
    {
        _service.UpdateUser(u => u with { Theme = ThemePreference.Dark });

        _store.SaveCount.ShouldBe(1);
        _store.Saved.User.Theme.ShouldBe(ThemePreference.Dark);
        var change = _changes.Single();
        change.UserChanged.ShouldBeTrue();
        change.RulesChanged.ShouldBeFalse();
        change.HotkeysChanged.ShouldBeFalse();
    }

    [Fact]
    public void Setting_the_same_value_does_not_write()
    {
        _service.UpdateUser(u => u with { Theme = u.Theme });

        _store.SaveCount.ShouldBe(0);
        _changes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 10)]
    [InlineData(5, 5)]
    public void Health_check_interval_is_kept_in_range(int requested, int expected)
    {
        _service.UpdateUser(u => u with { HealthCheckSeconds = requested });

        _service.Current.User.HealthCheckSeconds.ShouldBe(expected);
    }

    [Fact]
    public void Unknown_language_and_taskbar_only_mode_fall_back_to_what_exists()
    {
        var store = new FakeSettingsStore(SettingsDocument.CreateDefault(DateTimeOffset.UnixEpoch) with
        {
            User = new UserSettings { Language = "fr", HideMode = HideMode.TaskbarOnly },
        });

        var user = new SettingsService(store, _time).Current.User;

        user.Language.ShouldBe("es");
        user.HideMode.ShouldBe(HideMode.Hide);
    }

    [Fact]
    public void Updating_a_rule_stamps_its_modification_time()
    {
        var rule = _service.Current.Rules[1];

        _service.UpdateRule(rule.Id, r => r with { Enabled = true });

        var saved = _service.Current.Rules[1];
        saved.Enabled.ShouldBeTrue();
        saved.UpdatedAt.ShouldBe(_time.GetUtcNow());
        _changes.Single().RulesChanged.ShouldBeTrue();
    }

    [Fact]
    public void Rules_can_be_added_once_and_removed()
    {
        var rule = _service.CreateRule("Bloc de notas", "notepad.exe");

        _service.AddRule(rule);
        _service.AddRule(rule);
        _service.Current.Rules.Count(r => r.Id == rule.Id).ShouldBe(1);
        rule.TitleParser.ShouldBe(TitleParserKind.Generic);

        _service.RemoveRule(rule.Id);
        _service.Current.Rules.ShouldNotContain(r => r.Id == rule.Id);
    }

    [Fact]
    public void A_hotkey_is_replaced_by_command_and_can_be_reset()
    {
        var changed = new HotkeyBinding(HotkeyCommand.MergeAll, HotkeyModifiers.Control | HotkeyModifiers.Shift, 'G');

        _service.UpdateHotkey(changed);
        _service.Current.Hotkeys.Single(h => h.Command == HotkeyCommand.MergeAll).ShouldBe(changed);
        _changes.Single().HotkeysChanged.ShouldBeTrue();

        _service.ResetHotkeys();
        _service.Current.Hotkeys.ShouldBe(HotkeyBinding.Defaults);
    }
}
