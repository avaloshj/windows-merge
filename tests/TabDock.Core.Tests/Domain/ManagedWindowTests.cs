using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Tests.Domain;

public sealed class ManagedWindowTests
{
    public static TheoryData<ManagedWindowState, ManagedWindowState> ValidTransitions() => new()
    {
        { ManagedWindowState.Candidate, ManagedWindowState.Attaching },
        { ManagedWindowState.Attaching, ManagedWindowState.Attached },
        { ManagedWindowState.Attaching, ManagedWindowState.Detached },
        { ManagedWindowState.Attached, ManagedWindowState.Unresponsive },
        { ManagedWindowState.Attached, ManagedWindowState.Closing },
        { ManagedWindowState.Attached, ManagedWindowState.Detaching },
        { ManagedWindowState.Attached, ManagedWindowState.Gone },
        { ManagedWindowState.Unresponsive, ManagedWindowState.Attached },
        { ManagedWindowState.Unresponsive, ManagedWindowState.Gone },
        { ManagedWindowState.Closing, ManagedWindowState.Attached },
        { ManagedWindowState.Closing, ManagedWindowState.Gone },
        { ManagedWindowState.Detaching, ManagedWindowState.Detached },
    };

    public static TheoryData<ManagedWindowState, ManagedWindowState> InvalidTransitions() => new()
    {
        { ManagedWindowState.Candidate, ManagedWindowState.Attached },
        { ManagedWindowState.Candidate, ManagedWindowState.Unresponsive },
        { ManagedWindowState.Attaching, ManagedWindowState.Gone },
        { ManagedWindowState.Detaching, ManagedWindowState.Attached },
        { ManagedWindowState.Detached, ManagedWindowState.Attaching },
        { ManagedWindowState.Gone, ManagedWindowState.Attached },
        { ManagedWindowState.Gone, ManagedWindowState.Detaching },
    };

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void Valid_transitions_are_accepted(ManagedWindowState from, ManagedWindowState to)
    {
        ManagedWindow.CanTransition(from, to).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void Invalid_transitions_are_rejected_without_throwing(ManagedWindowState from, ManagedWindowState to)
    {
        ManagedWindow.CanTransition(from, to).ShouldBeFalse();
    }

    [Fact]
    public void Move_to_reports_failure_and_keeps_the_state()
    {
        var window = Create();

        var result = window.MoveTo(ManagedWindowState.Attached);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("window.invalidTransition");
        window.State.ShouldBe(ManagedWindowState.Candidate);
    }

    [Fact]
    public void Detached_and_gone_are_terminal()
    {
        foreach (var terminal in new[] { ManagedWindowState.Detached, ManagedWindowState.Gone })
        {
            foreach (var next in Enum.GetValues<ManagedWindowState>())
                ManagedWindow.CanTransition(terminal, next).ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData(ManagedWindowState.Attached, true)]
    [InlineData(ManagedWindowState.Unresponsive, true)]
    [InlineData(ManagedWindowState.Closing, true)]
    [InlineData(ManagedWindowState.Attaching, false)]
    [InlineData(ManagedWindowState.Detached, false)]
    public void Only_live_states_count_as_in_group(ManagedWindowState state, bool expected)
    {
        var window = Create();
        foreach (var step in PathTo(state))
            window.MoveTo(step).IsSuccess.ShouldBeTrue();

        window.IsInGroup.ShouldBe(expected);
    }

    private static ManagedWindow Create() => new(
        new WindowSnapshot(
            new WindowIdentity(new WindowHandle(1), 1000, null),
            new WindowPlacementData(0, 1, 0, 0, 0, 0, new PixelRect(0, 0, 800, 600)),
            new PixelRect(0, 0, 800, 600),
            WasVisible: true,
            WasMaximized: false,
            WasMinimized: false,
            DateTimeOffset.UnixEpoch),
        "proyecto-alfa");

    private static ManagedWindowState[] PathTo(ManagedWindowState state) => state switch
    {
        ManagedWindowState.Attaching => [ManagedWindowState.Attaching],
        ManagedWindowState.Attached => [ManagedWindowState.Attaching, ManagedWindowState.Attached],
        ManagedWindowState.Unresponsive => [ManagedWindowState.Attaching, ManagedWindowState.Attached, ManagedWindowState.Unresponsive],
        ManagedWindowState.Closing => [ManagedWindowState.Attaching, ManagedWindowState.Attached, ManagedWindowState.Closing],
        ManagedWindowState.Detached => [ManagedWindowState.Attaching, ManagedWindowState.Detached],
        _ => [],
    };
}
