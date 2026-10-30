using TabDock.App.ViewModels;
using TabDock.Core.Domain;

namespace TabDock.App.Tests.ViewModels;

public sealed class HostFrameViewModelTests
{
    [Fact]
    public void Load_selects_the_first_tab()
    {
        var vm = CreateLoaded();

        vm.SelectedTab!.DisplayName.ShouldBe("proyecto-alfa");
        vm.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void New_frame_without_tabs_reports_empty_state()
    {
        var vm = new HostFrameViewModel();

        vm.IsEmpty.ShouldBeTrue();
        vm.HasNoResults.ShouldBeFalse();
    }

    [Fact]
    public void Search_filters_by_name_and_subtitle_ignoring_case()
    {
        var vm = CreateLoaded();

        vm.SearchText = "BETA";
        Visible(vm).ShouldBe(["proyecto-beta"]);

        vm.SearchText = "server.ts";
        Visible(vm).ShouldBe(["demo-api"]);
    }

    [Fact]
    public void Search_without_matches_reports_no_results_and_clear_restores_all()
    {
        var vm = CreateLoaded();

        vm.SearchText = "inexistente";
        vm.HasNoResults.ShouldBeTrue();

        vm.ClearSearchCommand.Execute(null);
        vm.HasNoResults.ShouldBeFalse();
        Visible(vm).Count.ShouldBe(3);
    }

    [Fact]
    public void Closing_the_selected_tab_selects_the_next_one_and_removal_happens_after_the_animation()
    {
        var vm = CreateLoaded();
        var first = vm.Tabs[0];

        vm.CloseTabCommand.Execute(first);

        first.IsClosing.ShouldBeTrue();
        vm.Tabs.ShouldContain(first);
        vm.SelectedTab!.DisplayName.ShouldBe("proyecto-beta");

        vm.RemoveTabCommand.Execute(first);
        vm.Tabs.ShouldNotContain(first);
    }

    [Fact]
    public void With_a_close_handler_the_tab_stays_until_the_window_really_closes()
    {
        var vm = CreateLoaded();
        var requested = new List<string>();
        vm.TabCloseRequested += (_, e) => requested.Add(e.Tab.DisplayName);

        vm.CloseTabCommand.Execute(vm.Tabs[1]);

        requested.ShouldBe(["proyecto-beta"]);
        vm.Tabs[1].IsClosing.ShouldBeFalse();

        vm.BeginRemoveTab(vm.Tabs[1]);
        vm.Tabs[1].IsClosing.ShouldBeTrue();
    }

    [Fact]
    public void Tab_keeps_the_identifier_of_its_managed_window()
    {
        var id = Guid.NewGuid();

        new TabViewModel(id, "proyecto-alfa", "#3B82F6").Id.ShouldBe(id);
    }

    [Fact]
    public void Closing_the_last_tab_selects_the_previous_one()
    {
        var vm = CreateLoaded();
        vm.SelectedTab = vm.Tabs[2];

        vm.CloseTabCommand.Execute(vm.Tabs[2]);

        vm.SelectedTab!.DisplayName.ShouldBe("proyecto-beta");
    }

    [Fact]
    public void Removing_every_tab_reports_empty_state()
    {
        var vm = CreateLoaded();

        foreach (var tab in vm.Tabs.ToList())
            vm.RemoveTabCommand.Execute(tab);

        vm.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData("1", "proyecto-alfa")]
    [InlineData("3", "demo-api")]
    public void Select_tab_at_uses_visual_position(string position, string expected)
    {
        var vm = CreateLoaded();

        vm.SelectTabAtCommand.Execute(position);

        vm.SelectedTab!.DisplayName.ShouldBe(expected);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("9")]
    [InlineData("x")]
    public void Select_tab_at_ignores_invalid_positions(string position)
    {
        var vm = CreateLoaded();

        vm.SelectTabAtCommand.Execute(position);

        vm.SelectedTab!.DisplayName.ShouldBe("proyecto-alfa");
    }

    [Theory]
    [InlineData(100, HostFrameViewModel.MinSidebarWidth)]
    [InlineData(500, HostFrameViewModel.MaxSidebarWidth)]
    [InlineData(300, 300)]
    public void Sidebar_width_is_clamped(double requested, double expected)
    {
        var vm = new HostFrameViewModel { SidebarWidth = requested };

        vm.SidebarWidth.ShouldBe(expected);
    }

    [Fact]
    public void Toggle_sidebar_keeps_the_expanded_width()
    {
        var vm = new HostFrameViewModel { SidebarWidth = 300 };

        vm.ToggleSidebarCommand.Execute(null);
        vm.IsSidebarCollapsed.ShouldBeTrue();
        vm.ToggleSidebarCommand.Execute(null);

        vm.IsSidebarCollapsed.ShouldBeFalse();
        vm.SidebarWidth.ShouldBe(300);
    }

    [Fact]
    public void Automation_name_announces_states_shown_only_visually()
    {
        var tab = new TabViewModel("demo-api", "#3B82F6") { IsDirty = true, RemoteKind = RemoteKind.Ssh };

        tab.AutomationName.ShouldStartWith("demo-api, ");
        tab.AutomationName.ShouldContain("SSH");
    }

    private static HostFrameViewModel CreateLoaded()
    {
        var vm = new HostFrameViewModel();
        vm.Load(
            "Grupo ficticio",
            [
                new TabViewModel("proyecto-alfa", "#3B82F6") { Subtitle = "index.ts" },
                new TabViewModel("proyecto-beta", "#10B981") { Subtitle = "README.md" },
                new TabViewModel("demo-api", "#F59E0B") { Subtitle = "server.ts", RemoteKind = RemoteKind.Ssh },
            ]);
        return vm;
    }

    private static List<string> Visible(HostFrameViewModel vm) =>
        [.. vm.TabsView.Cast<TabViewModel>().Select(t => t.DisplayName)];
}
