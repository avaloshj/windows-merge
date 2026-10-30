using TabDock.App.ViewModels;

namespace TabDock.App.Tests.ViewModels;

/// <summary>Reordenar, fijar, renombrar, acciones de pestaña y búsqueda rápida (Fase 5).</summary>
public sealed class TabFeaturesTests
{
    private readonly HostFrameViewModel _vm = new();
    private readonly List<TabActionEventArgs> _actions = [];

    public TabFeaturesTests()
    {
        _vm.TabActionRequested += (_, e) => _actions.Add(e);
    }

    [Fact]
    public void Insert_puts_pinned_first_then_saved_order_then_the_rest()
    {
        _vm.InsertTab(Tab("sin-orden"));
        _vm.InsertTab(Tab("segundo", order: 1));
        _vm.InsertTab(Tab("fijado", pinned: true));
        _vm.InsertTab(Tab("primero", order: 0));

        Names().ShouldBe(["fijado", "primero", "segundo", "sin-orden"]);
    }

    [Fact]
    public void Moving_stays_within_the_pinned_or_unpinned_section()
    {
        var pinned = Tab("fijado", pinned: true);
        var alfa = Tab("alfa");
        var beta = Tab("beta");
        foreach (var t in new[] { pinned, alfa, beta })
            _vm.InsertTab(t);

        _vm.MoveTab(beta, 0).ShouldBeTrue();
        Names().ShouldBe(["fijado", "beta", "alfa"]);

        _vm.MoveTab(pinned, 2).ShouldBeFalse();
        Names().ShouldBe(["fijado", "beta", "alfa"]);
    }

    [Fact]
    public void Completing_a_drag_requests_saving_the_order()
    {
        var alfa = Tab("alfa");
        _vm.InsertTab(alfa);

        _vm.CompleteReorder(alfa);

        _actions.Single().Action.ShouldBe(TabAction.Reorder);
    }

    [Fact]
    public void Pinning_moves_the_tab_to_the_end_of_the_pinned_section_and_saves()
    {
        var a = Tab("a", pinned: true);
        var b = Tab("b");
        var c = Tab("c");
        foreach (var t in new[] { a, b, c })
            _vm.InsertTab(t);

        _vm.TogglePin(c);

        Names().ShouldBe(["a", "c", "b"]);
        c.IsPinned.ShouldBeTrue();
        _actions.Select(x => x.Action).ShouldBe([TabAction.TogglePin, TabAction.Reorder]);

        _vm.TogglePin(a);
        Names().ShouldBe(["c", "a", "b"]);
    }

    [Theory]
    [InlineData("  Alfa nueva  ", "Alfa nueva")]
    [InlineData("   ", null)]
    public void Committed_rename_is_trimmed_and_empty_means_no_alias(string typed, string? expected)
    {
        var alfa = Tab("proyecto-alfa");
        _vm.InsertTab(alfa);

        _vm.Rename(alfa);
        alfa.IsRenaming.ShouldBeTrue();
        alfa.RenameText.ShouldBe("proyecto-alfa");
        alfa.RenameText = typed;
        alfa.CommitRename();

        alfa.IsRenaming.ShouldBeFalse();
        var action = _actions.Single();
        action.Action.ShouldBe(TabAction.Rename);
        action.Value.ShouldBe(expected);
    }

    [Fact]
    public void Cancelled_rename_raises_nothing_and_only_one_tab_renames_at_a_time()
    {
        var alfa = Tab("alfa");
        var beta = Tab("beta");
        _vm.InsertTab(alfa);
        _vm.InsertTab(beta);

        _vm.Rename(alfa);
        _vm.Rename(beta);
        alfa.IsRenaming.ShouldBeFalse();

        beta.CancelRename();
        _actions.ShouldBeEmpty();
    }

    [Fact]
    public void Color_detach_and_move_are_forwarded_with_their_values()
    {
        var alfa = Tab("alfa");
        var target = Guid.NewGuid();
        _vm.InsertTab(alfa);

        _vm.SetColor(alfa, "#EC4899");
        _vm.Detach(alfa, (900, 500));
        _vm.MoveToGroup(alfa, target);

        _actions.Select(a => (a.Action, a.Value)).ShouldBe([
            (TabAction.ChangeColor, (object?)"#EC4899"),
            (TabAction.Detach, (900, 500)),
            (TabAction.MoveToGroup, target),
        ]);
    }

    [Fact]
    public void Close_others_requests_closing_every_other_tab()
    {
        var closed = new List<string>();
        _vm.TabCloseRequested += (_, e) => closed.Add(e.Tab.DisplayName);
        var alfa = Tab("alfa");
        foreach (var t in new[] { alfa, Tab("beta"), Tab("gamma") })
            _vm.InsertTab(t);

        _vm.CloseOthers(alfa);

        closed.ShouldBe(["beta", "gamma"]);
    }

    [Fact]
    public void Reordering_is_disabled_while_searching()
    {
        _vm.CanReorder.ShouldBeTrue();
        _vm.SearchText = "alfa";
        _vm.CanReorder.ShouldBeFalse();
    }

    [Fact]
    public void Quick_search_ranks_fuzzy_matches_and_selects_the_best()
    {
        var group = Guid.NewGuid();
        var search = new QuickSearchViewModel(
        [
            new(Guid.NewGuid(), group, "proyecto-alfa", "index.ts", "Visual Studio Code", "#3B82F6"),
            new(Guid.NewGuid(), group, "demo-api", "server.ts", "Visual Studio Code", "#10B981"),
            new(Guid.NewGuid(), group, "panel-demo", "compose.yml", "Visual Studio Code", "#F59E0B"),
        ]);

        search.Results.Count.ShouldBe(3);

        search.Query = "api";
        search.Results.Select(r => r.Name).ShouldBe(["demo-api"]);
        search.Selected!.Name.ShouldBe("demo-api");

        search.Query = "server";
        search.Results.Single().Name.ShouldBe("demo-api");

        search.Query = "demo";
        search.Results.First().Name.ShouldBe("demo-api");

        search.Query = "zzz";
        search.HasNoResults.ShouldBeTrue();
    }

    [Fact]
    public void Quick_search_selection_wraps_around()
    {
        var group = Guid.NewGuid();
        var search = new QuickSearchViewModel(
        [
            new(Guid.NewGuid(), group, "alfa", "", "G", "#3B82F6"),
            new(Guid.NewGuid(), group, "beta", "", "G", "#10B981"),
        ]);

        search.MoveSelection(-1);
        search.Selected!.Name.ShouldBe("beta");
        search.MoveSelection(+1);
        search.Selected!.Name.ShouldBe("alfa");
    }

    private static TabViewModel Tab(string name, bool pinned = false, int? order = null) =>
        new(name, "#3B82F6") { ProjectName = name, IsPinned = pinned, SavedOrder = order };

    private List<string> Names() => [.. _vm.Tabs.Select(t => t.DisplayName)];
}
