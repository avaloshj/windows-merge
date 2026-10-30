using TabDock.Core.Abstractions;
using TabDock.Core.Services;

namespace TabDock.Core.Tests.Services;

public sealed class EligibilityPolicyTests
{
    private const int OwnProcessId = 4242;

    private static readonly WindowInfo s_normalWindow = new()
    {
        Handle = new WindowHandle(0x1234),
        ProcessId = 1000,
        ProcessStartTime = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        ExecutablePath = @"C:\Programas\EditorFicticio\Code.exe",
        ClassName = "Chrome_WidgetWin_1",
        Title = "proyecto-alfa - Visual Studio Code",
        ExtendedFrameBounds = new PixelRect(0, 0, 1280, 800),
        IsVisible = true,
        IsCloaked = false,
        HasOwner = false,
        IsToolWindow = false,
        IsProcessAccessible = true,
        IsElevatedProcess = false,
        ShowState = WindowShowState.Normal,
    };

    private readonly EligibilityPolicy _policy = new(new EligibilityOptions(), OwnProcessId);

    [Fact]
    public void Normal_application_window_is_eligible()
    {
        _policy.Evaluate(s_normalWindow).ShouldBe(EligibilityResult.Eligible);
    }

    [Theory]
    [InlineData("own-process", ExclusionReason.OwnProcess, EligibilityStatus.Excluded)]
    [InlineData("hidden", ExclusionReason.NotVisible, EligibilityStatus.Excluded)]
    [InlineData("cloaked", ExclusionReason.Cloaked, EligibilityStatus.Excluded)]
    [InlineData("owned", ExclusionReason.HasOwner, EligibilityStatus.Excluded)]
    [InlineData("tool-window", ExclusionReason.ToolWindow, EligibilityStatus.Excluded)]
    [InlineData("dialog-class", ExclusionReason.ExcludedClass, EligibilityStatus.Excluded)]
    [InlineData("menu-class", ExclusionReason.ExcludedClass, EligibilityStatus.Excluded)]
    [InlineData("empty-title", ExclusionReason.EmptyTitle, EligibilityStatus.Excluded)]
    [InlineData("blank-title", ExclusionReason.EmptyTitle, EligibilityStatus.Excluded)]
    [InlineData("too-narrow", ExclusionReason.TooSmall, EligibilityStatus.Excluded)]
    [InlineData("too-short", ExclusionReason.TooSmall, EligibilityStatus.Excluded)]
    [InlineData("inaccessible", ExclusionReason.ProcessInaccessible, EligibilityStatus.Disabled)]
    [InlineData("elevated", ExclusionReason.ElevatedProcess, EligibilityStatus.Disabled)]
    public void Ineligible_window_reports_status_and_reason(string scenario, ExclusionReason reason, EligibilityStatus status)
    {
        var result = _policy.Evaluate(Scenario(scenario));

        result.Status.ShouldBe(status);
        result.Reason.ShouldBe(reason);
        result.IsEligible.ShouldBeFalse();
    }

    [Fact]
    public void First_failing_rule_wins_so_diagnostics_are_stable()
    {
        var window = s_normalWindow with { IsVisible = false, IsToolWindow = true, Title = string.Empty };

        _policy.Evaluate(window).Reason.ShouldBe(ExclusionReason.NotVisible);
    }

    [Fact]
    public void Excluded_reasons_take_precedence_over_disabled_ones()
    {
        var window = s_normalWindow with { IsElevatedProcess = true, HasOwner = true };

        _policy.Evaluate(window).ShouldBe(EligibilityResult.Excluded(ExclusionReason.HasOwner));
    }

    [Fact]
    public void Minimized_window_is_not_judged_by_its_placeholder_bounds()
    {
        var window = s_normalWindow with
        {
            ShowState = WindowShowState.Minimized,
            ExtendedFrameBounds = new PixelRect(-32000, -32000, -31840, -31972),
        };

        _policy.Evaluate(window).ShouldBe(EligibilityResult.Eligible);
    }

    [Fact]
    public void Window_exactly_at_minimum_size_is_eligible()
    {
        var window = s_normalWindow with { ExtendedFrameBounds = new PixelRect(100, 100, 400, 300) };

        _policy.Evaluate(window).ShouldBe(EligibilityResult.Eligible);
    }

    [Fact]
    public void Excluded_class_names_are_case_insensitive()
    {
        var window = s_normalWindow with { ClassName = "PROGMAN" };

        _policy.Evaluate(window).Reason.ShouldBe(ExclusionReason.ExcludedClass);
    }

    [Fact]
    public void Custom_minimum_size_is_respected()
    {
        var policy = new EligibilityPolicy(new EligibilityOptions { MinWidth = 1500, MinHeight = 200 }, OwnProcessId);

        policy.Evaluate(s_normalWindow).Reason.ShouldBe(ExclusionReason.TooSmall);
    }

    private static WindowInfo Scenario(string name) => name switch
    {
        "own-process" => s_normalWindow with { ProcessId = OwnProcessId },
        "hidden" => s_normalWindow with { IsVisible = false },
        "cloaked" => s_normalWindow with { IsCloaked = true },
        "owned" => s_normalWindow with { HasOwner = true },
        "tool-window" => s_normalWindow with { IsToolWindow = true },
        "dialog-class" => s_normalWindow with { ClassName = "#32770" },
        "menu-class" => s_normalWindow with { ClassName = "#32768" },
        "empty-title" => s_normalWindow with { Title = string.Empty },
        "blank-title" => s_normalWindow with { Title = "   " },
        "too-narrow" => s_normalWindow with { ExtendedFrameBounds = new PixelRect(0, 0, 299, 800) },
        "too-short" => s_normalWindow with { ExtendedFrameBounds = new PixelRect(0, 0, 1280, 199) },
        "inaccessible" => s_normalWindow with { IsProcessAccessible = false, ExecutablePath = string.Empty },
        "elevated" => s_normalWindow with { IsElevatedProcess = true },
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Escenario desconocido."),
    };
}
