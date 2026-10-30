using TabDock.App.Tests.Fakes;
using TabDock.App.ViewModels;
using TabDock.Core.Services;

namespace TabDock.App.Tests.ViewModels;

public sealed class OnboardingViewModelTests
{
    private readonly InMemorySettingsStore _store = new();
    private readonly SettingsService _settings;
    private readonly OnboardingViewModel _vm;
    private int _completed;

    public OnboardingViewModelTests()
    {
        _settings = new SettingsService(_store, TimeProvider.System);
        _vm = new OnboardingViewModel(_settings, () => [new RunningProgram("Visual Studio Code", "Code.exe")]);
        _vm.Completed += (_, _) => _completed++;
    }

    [Fact]
    public void Starts_on_step_1_with_the_enabled_programs_selected_and_running_ones_marked()
    {
        _vm.Step.ShouldBe(1);
        _vm.StepText.ShouldContain("1");
        _vm.BackCommand.CanExecute(null).ShouldBeFalse();

        var vsCode = _vm.Programs.Single(p => p.DisplayName == "Visual Studio Code");
        vsCode.IsSelected.ShouldBeTrue();
        vsCode.IsRunning.ShouldBeTrue();
        _vm.Programs.Single(p => p.DisplayName == "Cursor").IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void Cannot_continue_without_any_program()
    {
        foreach (var program in _vm.Programs)
            program.IsSelected = false;

        _vm.NextCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void Leaving_step_1_saves_the_chosen_programs()
    {
        _vm.Programs.Single(p => p.DisplayName == "Cursor").IsSelected = true;

        _vm.Next();

        _vm.Step.ShouldBe(2);
        _store.Saved.Rules.Single(r => r.DisplayName == "Cursor").Enabled.ShouldBeTrue();
        _store.Saved.User.OnboardingCompleted.ShouldBeFalse();
    }

    [Fact]
    public void Finishing_on_step_3_marks_it_completed_once()
    {
        _vm.Next();
        _vm.Next();
        _vm.IsLastStep.ShouldBeTrue();

        _vm.Next();
        _vm.Skip();

        _store.Saved.User.OnboardingCompleted.ShouldBeTrue();
        _completed.ShouldBe(1);
    }

    [Fact]
    public void Skipping_also_marks_it_completed()
    {
        _vm.Skip();

        _vm.IsCompleted.ShouldBeTrue();
        _store.Saved.User.OnboardingCompleted.ShouldBeTrue();
    }

    [Fact]
    public void A_merge_only_counts_on_the_last_step()
    {
        _vm.NotifyMerged();
        _vm.HasMerged.ShouldBeFalse();

        _vm.Next();
        _vm.Next();
        _vm.NotifyMerged();

        _vm.HasMerged.ShouldBeTrue();
    }

    [Fact]
    public void The_suggested_vs_code_setting_uses_the_custom_title_bar_and_a_parseable_title()
    {
        OnboardingViewModel.VsCodeSnippet.ShouldContain("\"window.titleBarStyle\": \"custom\"");
        OnboardingViewModel.VsCodeSnippet.ShouldContain("${rootName}${separator}${activeEditorShort}${separator}${appName}");
    }
}
