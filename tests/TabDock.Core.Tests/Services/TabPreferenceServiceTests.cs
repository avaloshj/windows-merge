using Microsoft.Extensions.Time.Testing;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;
using TabDock.Core.Services;

namespace TabDock.Core.Tests.Services;

public sealed class TabPreferenceServiceTests
{
    private static readonly Guid s_rule = Guid.Parse("5d2c1b8e-0000-4000-8000-000000000001");

    private readonly InMemoryPreferences _store = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));
    private readonly TabPreferenceService _service;

    public TabPreferenceServiceTests()
    {
        _service = new TabPreferenceService(_store, _time);
        _service.Load();
    }

    [Fact]
    public void Preferences_are_found_by_rule_and_normalized_project_name()
    {
        _service.Update(s_rule, "Proyecto-Alfa", p => p with { Alias = "Alfa", ColorHex = "#EC4899", Pinned = true });

        var found = _service.Find(s_rule, "  proyecto-alfa ");

        found.ShouldNotBeNull();
        found.Alias.ShouldBe("Alfa");
        found.ColorHex.ShouldBe("#EC4899");
        found.Pinned.ShouldBeTrue();
        _service.Find(Guid.NewGuid(), "proyecto-alfa").ShouldBeNull();
    }

    [Fact]
    public void Every_change_is_saved()
    {
        _service.Update(s_rule, "proyecto-alfa", p => p with { Alias = "Alfa" });

        _store.Saved.Tabs.Single().Alias.ShouldBe("Alfa");
    }

    [Fact]
    public void Windows_without_project_have_no_preferences()
    {
        _service.Update(s_rule, "", p => p with { Alias = "x" }).ShouldBeNull();
        _service.Count.ShouldBe(0);
    }

    [Fact]
    public void Order_is_stored_for_each_project()
    {
        _service.SetOrder(s_rule, ["proyecto-beta", "proyecto-alfa", ""]);

        _service.Find(s_rule, "proyecto-beta")!.Order.ShouldBe(0);
        _service.Find(s_rule, "proyecto-alfa")!.Order.ShouldBe(1);
    }

    [Fact]
    public void Resetting_everything_removes_the_entry_from_disk()
    {
        _service.Update(s_rule, "proyecto-alfa", p => p with { Alias = "Alfa" });
        _service.Update(s_rule, "proyecto-alfa", p => p with { Alias = null });

        _store.Saved.Tabs.ShouldBeEmpty();
    }

    [Fact]
    public void Unseen_for_90_days_is_marked_and_purged_on_the_following_start()
    {
        _service.Update(s_rule, "proyecto-alfa", p => p with { Alias = "Alfa" });
        _service.Update(s_rule, "proyecto-beta", p => p with { Alias = "Beta" });

        _time.Advance(TimeSpan.FromDays(60));
        _service.Touch(s_rule, "proyecto-beta");
        _time.Advance(TimeSpan.FromDays(31));

        var second = new TabPreferenceService(_store, _time);
        second.Load();
        second.Find(s_rule, "proyecto-alfa").ShouldBeNull();
        second.Find(s_rule, "proyecto-beta").ShouldNotBeNull();
        _store.Saved.Tabs.Single(t => t.ProjectKey == "proyecto-alfa").DeletedAt.ShouldNotBeNull();

        var third = new TabPreferenceService(_store, _time);
        third.Load();
        _store.Saved.Tabs.Select(t => t.ProjectKey).ShouldBe(["proyecto-beta"]);
    }

    [Fact]
    public void Using_a_marked_project_again_rescues_it()
    {
        _service.Update(s_rule, "proyecto-alfa", p => p with { Alias = "Alfa" });
        _time.Advance(TimeSpan.FromDays(91));
        var second = new TabPreferenceService(_store, _time);
        second.Load();

        second.Update(s_rule, "proyecto-alfa", p => p with { Pinned = true });

        second.Find(s_rule, "proyecto-alfa")!.Alias.ShouldBe("Alfa");
        _store.Saved.Tabs.Single().DeletedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("alfa", "proyecto-alfa", true)]
    [InlineData("pa", "proyecto-alfa", true)]
    [InlineData("indice", "índice.md", true)]
    [InlineData("ALFA", "proyecto-alfa", true)]
    [InlineData("xyz", "proyecto-alfa", false)]
    [InlineData("afla", "proyecto-alfa", false)]
    public void Fuzzy_matching_requires_letters_in_order(string query, string text, bool matches)
    {
        (FuzzyMatcher.Score(query, text) is not null).ShouldBe(matches);
    }

    [Fact]
    public void Fuzzy_matching_prefers_word_starts_and_consecutive_letters()
    {
        var wordStart = FuzzyMatcher.Score("api", "demo-api")!.Value;
        var scattered = FuzzyMatcher.Score("api", "zapatilla")!.Value;
        var prefix = FuzzyMatcher.Score("demo", "demo-api")!.Value;
        var inside = FuzzyMatcher.Score("demo", "mi-demo-api")!.Value;

        wordStart.ShouldBeGreaterThan(scattered);
        prefix.ShouldBeGreaterThan(inside);
        FuzzyMatcher.Score("", "lo-que-sea").ShouldBe(0);
    }

    private sealed class InMemoryPreferences : IPreferencesStore
    {
        public PreferencesDocument Saved { get; private set; } = PreferencesDocument.Empty;

        public PreferencesDocument Load() => Saved;

        public void Save(PreferencesDocument preferences) => Saved = preferences;
    }
}
