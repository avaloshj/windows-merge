using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;
using TabDock.Core.Services;
using TabDock.Core.Tests.Fakes;

namespace TabDock.Core.Tests.Services;

public sealed class TabGroupSessionTests : IDisposable
{
    private static readonly PixelRect s_original = new(50, 60, 850, 660);

    private readonly List<string> _journal = [];
    private readonly FakeWindowSystem _windows;
    private readonly FakeHostFrame _frame = new();
    private readonly FakeStateStore _store;
    private readonly FakeTimeProvider _time = new();
    private readonly FakeVirtualDesktops _desktops = new();
    private readonly TabGroupSession _session;

    public TabGroupSessionTests()
    {
        _windows = new FakeWindowSystem(_journal);
        _store = new FakeStateStore(_journal);
        _session = new TabGroupSession(_windows, _desktops, _frame, new RuntimeStateTracker(_store, ownerProcessId: 1), _time, NullLogger<TabGroupSession>.Instance);
    }

    public void Dispose() => _session.Dispose();

    [Fact]
    public void First_window_is_persisted_before_it_is_moved_then_shown_in_the_slot()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);

        var result = _session.Attach(_windows.Info(alfa));

        result.IsSuccess.ShouldBeTrue();
        _journal.IndexOf("save:1").ShouldBeLessThan(_journal.IndexOf("show:1"));
        _journal.IndexOf("save:1").ShouldBeLessThan(_journal.IndexOf("place:1"));
        alfa.Visible.ShouldBeTrue();
        alfa.Bounds.ShouldBe(_frame.SlotBounds);
        _journal.ShouldContain("above:9999>1");
        _frame.HoleOpen.ShouldBeTrue();
        _session.Active!.Handle.ShouldBe(alfa.Handle);
        _store.Current.Windows.Count.ShouldBe(1);
        _store.Current.CleanShutdown.ShouldBeFalse();
    }

    [Fact]
    public void Further_windows_are_persisted_then_hidden_and_the_active_one_does_not_change()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);
        var beta = _windows.Add(2, "proyecto-beta", s_original);
        _session.Attach(_windows.Info(alfa));

        _session.Attach(_windows.Info(beta));

        _journal.IndexOf("save:2").ShouldBeLessThan(_journal.IndexOf("hide:2"));
        beta.Visible.ShouldBeFalse();
        beta.Bounds.ShouldBe(s_original);
        _session.Active!.Handle.ShouldBe(alfa.Handle);
        _session.Windows.Count.ShouldBe(2);
    }

    [Fact]
    public void A_new_window_attached_as_active_is_shown_without_being_hidden_first()
    {
        var (alfa, _) = AttachTwo();
        var gamma = _windows.Add(3, "proyecto-gamma", s_original);
        _journal.Clear();

        _session.Attach(_windows.Info(gamma), activate: true).IsSuccess.ShouldBeTrue();

        _journal.ShouldNotContain("hide:3");
        _journal.IndexOf("show:3").ShouldBeLessThan(_journal.IndexOf("hide:1"));
        gamma.Bounds.ShouldBe(_frame.SlotBounds);
        alfa.Visible.ShouldBeFalse();
        _session.Active!.Handle.ShouldBe(gamma.Handle);
    }

    [Fact]
    public void Attaching_the_same_window_twice_does_not_duplicate_it()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);

        var first = _session.Attach(_windows.Info(alfa));
        var second = _session.Attach(_windows.Info(alfa));

        second.Value.ShouldBeSameAs(first.Value);
        _session.Windows.Count.ShouldBe(1);
        _journal.Count(e => e == "capture:1").ShouldBe(1);
    }

    [Fact]
    public void If_the_state_cannot_be_saved_the_window_is_not_touched()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);
        _store.FailSave = true;

        var result = _session.Attach(_windows.Info(alfa));

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("state.saveFailed");
        _journal.ShouldNotContain(e => e.StartsWith("show", StringComparison.Ordinal) || e.StartsWith("hide", StringComparison.Ordinal) || e.StartsWith("place", StringComparison.Ordinal));
        _session.Windows.ShouldBeEmpty();
    }

    [Fact]
    public void If_placing_fails_the_window_is_restored_and_forgotten()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);
        _windows.FailPlace = true;

        var result = _session.Attach(_windows.Info(alfa));

        result.IsSuccess.ShouldBeFalse();
        result.Error.Win32Error.ShouldBe(5);
        _journal.ShouldContain("restore:1");
        alfa.Bounds.ShouldBe(s_original);
        _session.Windows.ShouldBeEmpty();
        _store.Current.Windows.ShouldBeEmpty();
    }

    [Fact]
    public void The_frame_itself_cannot_be_captured()
    {
        var self = _windows.Add((int)_frame.Handle.Value, "TabDock", s_original);

        _session.Attach(_windows.Info(self)).Error!.Code.ShouldBe("window.isFrame");
    }

    [Fact]
    public void Activating_shows_the_target_before_hiding_the_previous_and_focuses_it_last()
    {
        var (alfa, beta) = AttachTwo();
        _journal.Clear();

        var result = _session.Activate(_session.Windows[1].Id);

        result.IsSuccess.ShouldBeTrue();
        _journal.IndexOf("show:2").ShouldBeLessThan(_journal.IndexOf("hide:1"));
        _journal.IndexOf("hide:1").ShouldBeLessThan(_journal.IndexOf("focus:2"));
        beta.Visible.ShouldBeTrue();
        beta.Bounds.ShouldBe(_frame.SlotBounds);
        alfa.Visible.ShouldBeFalse();
        _session.Active!.Handle.ShouldBe(beta.Handle);
    }

    [Fact]
    public void Denied_foreground_does_not_undo_the_switch()
    {
        var (_, beta) = AttachTwo();
        _windows.DenyForeground = true;

        _session.Activate(_session.Windows[1].Id).IsSuccess.ShouldBeTrue();

        _session.Active!.Handle.ShouldBe(beta.Handle);
    }

    [Fact]
    public void When_the_active_window_is_destroyed_the_most_recent_one_takes_over()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);
        var beta = _windows.Add(2, "proyecto-beta", s_original);
        var gamma = _windows.Add(3, "proyecto-gamma", s_original);
        foreach (var w in new[] { alfa, beta, gamma })
            _session.Attach(_windows.Info(w));
        _session.Activate(_session.Windows[2].Id);   // gamma
        _session.Activate(_session.Windows[1].Id);   // beta (MRU: beta, gamma, alfa)
        var removed = new List<WindowHandle>();
        _session.WindowRemoved += (_, e) => removed.Add(e.Window.Handle);

        beta.Alive = false;
        _session.HandleEvent(new WindowEvent(WindowEventKind.Destroyed, beta.Handle));

        _session.Active!.Handle.ShouldBe(gamma.Handle);
        gamma.Visible.ShouldBeTrue();
        removed.ShouldBe([beta.Handle]);
        _store.Current.Windows.Count.ShouldBe(2);
    }

    [Fact]
    public void A_destroy_event_for_a_window_that_still_exists_is_ignored()
    {
        var (alfa, beta) = AttachTwo();
        _session.Activate(_session.Windows[1].Id);

        // Como Chromium al ocultar la pestaña inactiva: avisa de "destruida" pero la ventana sigue viva.
        _session.HandleEvent(new WindowEvent(WindowEventKind.Destroyed, alfa.Handle));
        _time.Advance(TimeSpan.FromSeconds(1));

        _session.Windows.Count.ShouldBe(2);
        _session.Active!.Handle.ShouldBe(beta.Handle);
        _store.Current.Windows.Count.ShouldBe(2);
    }

    [Fact]
    public void A_destroy_event_that_arrives_while_the_window_is_still_closing_is_confirmed_shortly_after()
    {
        var (alfa, _) = AttachTwo();

        // El aviso llega al empezar a destruirla: aún existe; poco después ya no.
        _session.HandleEvent(new WindowEvent(WindowEventKind.Destroyed, alfa.Handle));
        _session.Windows.Count.ShouldBe(2);

        alfa.Alive = false;
        _time.Advance(TimeSpan.FromMilliseconds(300));

        _session.Windows.Count.ShouldBe(1);
        _store.Current.Windows.Count.ShouldBe(1);
    }

    [Fact]
    public void Destroying_the_last_window_empties_the_group()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);
        _session.Attach(_windows.Info(alfa));
        var emptied = false;
        _session.Emptied += (_, _) => emptied = true;

        alfa.Alive = false;
        _session.HandleEvent(new WindowEvent(WindowEventKind.Destroyed, alfa.Handle));

        emptied.ShouldBeTrue();
        _store.Current.CleanShutdown.ShouldBeTrue();
    }

    [Fact]
    public void A_hidden_window_shown_by_its_application_becomes_the_active_tab()
    {
        var (_, beta) = AttachTwo();

        beta.Visible = true;
        _session.HandleEvent(new WindowEvent(WindowEventKind.Shown, beta.Handle));

        _session.Active!.Handle.ShouldBe(beta.Handle);
    }

    [Fact]
    public void Minimizing_the_frame_hides_the_active_window_and_restoring_brings_it_back()
    {
        var (alfa, _) = AttachTwo();

        _frame.Minimize();
        alfa.Visible.ShouldBeFalse();

        _frame.Restore();
        alfa.Visible.ShouldBeTrue();
        alfa.Bounds.ShouldBe(_frame.SlotBounds);
    }

    [Fact]
    public void Moving_the_frame_moves_the_active_window()
    {
        var (alfa, beta) = AttachTwo();
        var moved = new PixelRect(400, 200, 1400, 950);

        _frame.MoveSlot(moved);

        alfa.Bounds.ShouldBe(moved);
        beta.Bounds.ShouldBe(s_original);
    }

    [Fact]
    public void Maximizing_the_active_window_maximizes_the_frame()
    {
        var (alfa, _) = AttachTwo();

        alfa.ShowState = WindowShowState.Maximized;
        _session.HandleEvent(new WindowEvent(WindowEventKind.LocationChanged, alfa.Handle));

        _frame.IsMaximized.ShouldBeTrue();
        alfa.ShowState.ShouldBe(WindowShowState.Normal);
    }

    [Fact]
    public void After_maximizing_the_frame_the_window_goes_back_on_top_with_focus()
    {
        var (alfa, _) = AttachTwo();
        _journal.Clear();

        alfa.ShowState = WindowShowState.Maximized;
        _session.HandleEvent(new WindowEvent(WindowEventKind.LocationChanged, alfa.Handle));

        // Maximizar el marco lo activa: el foco vuelve a la ventana y, después, el marco se pone justo encima.
        var focus = _journal.LastIndexOf($"focus:{alfa.Handle.Value}");
        focus.ShouldBeGreaterThanOrEqualTo(0);
        _journal.LastIndexOf($"above:9999>{alfa.Handle.Value}").ShouldBeGreaterThan(focus);
    }

    [Fact]
    public void If_the_frame_ends_up_below_the_active_window_it_goes_back_on_top()
    {
        var (alfa, _) = AttachTwo();
        _journal.Clear();

        _windows.WindowAboveFrame = true;
        _frame.RaiseZOrderChanged();

        _journal.ShouldBe([$"above:9999>{alfa.Handle.Value}"]);
    }

    [Fact]
    public void Clicking_the_active_window_through_the_hole_puts_the_frame_back_on_top()
    {
        var (alfa, _) = AttachTwo();
        _journal.Clear();

        _session.HandleEvent(new WindowEvent(WindowEventKind.Foreground, alfa.Handle));

        _journal.ShouldContain($"above:9999>{alfa.Handle.Value}");
    }

    [Fact]
    public void A_late_foreground_event_of_the_previous_tab_does_not_undo_the_switch()
    {
        var (alfa, beta) = AttachTwo();
        _session.Activate(_session.Windows[1].Id);   // beta activa y en primer plano

        // Clic en la pestaña alfa; después llega, con retraso, el aviso de que beta pasó a primer plano.
        _session.Activate(_session.Windows[0].Id);
        _session.HandleEvent(new WindowEvent(WindowEventKind.Foreground, beta.Handle));

        _session.Active!.Handle.ShouldBe(alfa.Handle);
    }

    [Fact]
    public void A_late_shown_event_of_a_tab_that_was_hidden_again_is_ignored()
    {
        var (alfa, beta) = AttachTwo();

        // Cambios rápidos: TabDock mostró beta y la volvió a ocultar; el aviso de que se mostró llega ahora.
        _session.Activate(_session.Windows[1].Id);
        _session.Activate(_session.Windows[0].Id);
        beta.Visible.ShouldBeFalse();
        _session.HandleEvent(new WindowEvent(WindowEventKind.Shown, beta.Handle));

        _session.Active!.Handle.ShouldBe(alfa.Handle);
    }

    [Fact]
    public void A_hidden_tab_shown_by_its_own_program_is_activated()
    {
        var (_, beta) = AttachTwo();

        beta.Visible = true;   // p. ej. "code archivo.txt" la mostró
        _session.HandleEvent(new WindowEvent(WindowEventKind.Shown, beta.Handle));

        _session.Active!.Handle.ShouldBe(beta.Handle);
    }

    [Fact]
    public void A_foreground_event_of_a_hidden_tab_that_really_is_in_front_activates_it()
    {
        var (_, beta) = AttachTwo();
        _windows.Foreground = beta.Handle;   // p. ej. "code archivo.txt" la trajo al frente

        _session.HandleEvent(new WindowEvent(WindowEventKind.Foreground, beta.Handle));

        _session.Active!.Handle.ShouldBe(beta.Handle);
    }

    [Fact]
    public void The_hole_closes_when_no_window_is_left()
    {
        var (alfa, beta) = AttachTwo();

        beta.Alive = false;
        _session.HandleEvent(new WindowEvent(WindowEventKind.Destroyed, beta.Handle));
        _frame.HoleOpen.ShouldBeTrue();

        alfa.Alive = false;
        _session.HandleEvent(new WindowEvent(WindowEventKind.Destroyed, alfa.Handle));
        _frame.HoleOpen.ShouldBeFalse();
    }

    [Fact]
    public void A_frame_reorder_that_keeps_the_window_on_top_does_nothing()
    {
        AttachTwo();
        _journal.Clear();

        _frame.RaiseZOrderChanged();

        _journal.ShouldBeEmpty();
    }

    [Fact]
    public void A_native_title_bar_is_hidden_under_the_frame_bar()
    {
        var alfa = _windows.Add(1, "proyecto-alfa - Bloc de notas", s_original);
        alfa.CaptionInset = 31;
        var slotTop = _frame.SlotBounds.Top;

        _session.Attach(_windows.Info(alfa)).IsSuccess.ShouldBeTrue();

        // El marco muestra su barra (el hueco baja) y la ventana sube lo que mide su barra nativa: su contenido ocupa
        // el hueco y su barra queda bajo la del marco.
        _frame.OwnCaptionVisible.ShouldBeTrue();
        _frame.SlotBounds.Top.ShouldBe(slotTop + FakeHostFrame.CaptionRowHeight);
        alfa.Bounds.ShouldBe(new PixelRect(_frame.SlotBounds.Left, _frame.SlotBounds.Top - 31, _frame.SlotBounds.Right, _frame.SlotBounds.Bottom));
    }

    [Fact]
    public void A_window_that_draws_its_own_title_bar_keeps_it_as_the_group_bar()
    {
        var alfa = _windows.Add(1, "proyecto-alfa - Visual Studio Code", s_original);
        alfa.CaptionInset = 0;

        _session.Attach(_windows.Info(alfa)).IsSuccess.ShouldBeTrue();

        _frame.OwnCaptionVisible.ShouldBeFalse();
        alfa.Bounds.ShouldBe(_frame.SlotBounds);
    }

    [Fact]
    public void The_frame_bar_follows_the_active_tab()
    {
        var code = _windows.Add(1, "proyecto-alfa - Visual Studio Code", s_original);
        var notas = _windows.Add(2, "notas - Bloc de notas", s_original);
        notas.CaptionInset = 31;
        _session.Attach(_windows.Info(code)).IsSuccess.ShouldBeTrue();
        _session.Attach(_windows.Info(notas)).IsSuccess.ShouldBeTrue();

        _session.Activate(_session.Windows[1].Id);
        _frame.OwnCaptionVisible.ShouldBeTrue();

        _session.Activate(_session.Windows[0].Id);
        _frame.OwnCaptionVisible.ShouldBeFalse();
        code.Bounds.ShouldBe(_frame.SlotBounds);
    }

    [Fact]
    public void A_window_with_its_own_title_bar_loses_its_shadow_while_grouped()
    {
        var alfa = _windows.Add(1, "proyecto-alfa - Visual Studio Code", s_original);
        alfa.OwnChrome = true;
        var beta = _windows.Add(2, "proyecto-beta - Visual Studio Code", s_original);

        _session.Attach(_windows.Info(alfa)).IsSuccess.ShouldBeTrue();
        _session.Attach(_windows.Info(beta)).IsSuccess.ShouldBeTrue();

        _journal.ShouldContain("shadow:1:off");
        _journal.ShouldNotContain(e => e.StartsWith("shadow:2", StringComparison.Ordinal));
        _journal.IndexOf("save:1").ShouldBeLessThan(_journal.IndexOf("shadow:1:off"));

        _session.DetachAll();
        _journal.ShouldContain("shadow:1:on");
    }

    [Fact]
    public void Maximizing_the_active_window_again_restores_the_frame()
    {
        var (alfa, _) = AttachTwo();
        _frame.IsMaximized = true;

        alfa.ShowState = WindowShowState.Maximized;
        _session.HandleEvent(new WindowEvent(WindowEventKind.LocationChanged, alfa.Handle));

        _frame.IsMaximized.ShouldBeFalse();
        alfa.ShowState.ShouldBe(WindowShowState.Normal);
    }

    [Fact]
    public void Dragging_the_active_window_moves_the_frame_with_it()
    {
        var (alfa, _) = AttachTwo();
        var dragged = new PixelRect(500, 300, 1700, 1060);

        _session.HandleEvent(new WindowEvent(WindowEventKind.MoveSizeStart, alfa.Handle));
        alfa.Bounds = dragged;
        _session.HandleEvent(new WindowEvent(WindowEventKind.LocationChanged, alfa.Handle));

        _frame.MovedTo.ShouldBe([dragged]);
        alfa.Bounds.ShouldBe(dragged);
    }

    [Fact]
    public void Moved_by_other_means_the_active_window_is_put_back_in_the_slot()
    {
        var (alfa, _) = AttachTwo();
        _time.Advance(TimeSpan.FromSeconds(1));

        alfa.Bounds = new PixelRect(120, 80, 980, 900);   // movida por otra vía, sin tocar bordes del área de trabajo
        _session.HandleEvent(new WindowEvent(WindowEventKind.LocationChanged, alfa.Handle));

        alfa.Bounds.ShouldBe(_frame.SlotBounds);
        _frame.MovedTo.ShouldBeEmpty();
    }

    [Fact]
    public void A_window_that_resizes_itself_right_after_placement_is_put_back()
    {
        var (alfa, _) = AttachTwo();

        // Al pasar a un monitor con otra escala, la aplicación aplica el tamaño sugerido por WM_DPICHANGED (×0,8).
        var slot = _frame.SlotBounds;
        alfa.Bounds = new PixelRect(slot.Left, slot.Top, slot.Left + (int)(slot.Width * 0.8), slot.Top + (int)(slot.Height * 0.8));
        _session.HandleEvent(new WindowEvent(WindowEventKind.LocationChanged, alfa.Handle));

        alfa.Bounds.ShouldBe(slot);
    }

    [Fact]
    public void Corrections_stop_when_the_window_cannot_fit_the_slot()
    {
        var (alfa, _) = AttachTwo();
        var tooBig = new PixelRect(100, 100, 3100, 2100);   // no toca bordes del área de trabajo: no es Snap
        _journal.Clear();

        for (var i = 0; i < 10; i++)
        {
            alfa.Bounds = tooBig;   // tamaño mínimo mayor que el hueco: la ventana vuelve a crecer
            _session.HandleEvent(new WindowEvent(WindowEventKind.LocationChanged, alfa.Handle));
        }

        _journal.Count(e => e == "place:1").ShouldBe(3);
    }

    [Fact]
    public void Snapping_the_active_window_to_a_zone_moves_the_whole_group_there()
    {
        var (alfa, _) = AttachTwo();
        var leftHalf = new PixelRect(0, 0, 960, 1040);   // Win+Izquierda o una zona de Snap Layouts

        alfa.Bounds = leftHalf;
        _session.HandleEvent(new WindowEvent(WindowEventKind.LocationChanged, alfa.Handle));

        _frame.FrameBounds.ShouldBe([leftHalf]);
    }

    [Fact]
    public void Only_the_frame_keeps_a_taskbar_button()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);

        _session.Attach(_windows.Info(alfa));

        _journal.ShouldContain("taskbar:1:off");
        _journal.IndexOf("show:1").ShouldBeLessThan(_journal.IndexOf("taskbar:1:off"));
    }

    [Fact]
    public void The_taskbar_button_is_removed_again_when_the_window_comes_forward_or_renames()
    {
        var (alfa, _) = AttachTwo();
        _journal.Clear();

        _session.HandleEvent(new WindowEvent(WindowEventKind.Foreground, alfa.Handle));
        _session.HandleEvent(new WindowEvent(WindowEventKind.NameChanged, alfa.Handle));
        _session.CheckHealth();

        _journal.Count(e => e == "taskbar:1:off").ShouldBe(3);
    }

    [Fact]
    public void The_frame_follows_the_active_window_to_another_virtual_desktop()
    {
        var (alfa, _) = AttachTwo();
        var desktopA = Guid.NewGuid();
        var desktopB = Guid.NewGuid();
        _desktops.Desktops[_frame.Handle] = desktopA;
        _desktops.Desktops[alfa.Handle] = desktopB;

        _session.HandleEvent(new WindowEvent(WindowEventKind.Cloaked, alfa.Handle));

        _desktops.Desktops[_frame.Handle].ShouldBe(desktopB);
    }

    [Fact]
    public void Activating_the_frame_from_outside_gives_focus_to_the_active_window()
    {
        AttachTwo();
        _journal.Clear();

        _frame.Activate(forwardFocus: false);
        _journal.ShouldNotContain("focus:1");

        _frame.Activate(forwardFocus: true);
        _journal.ShouldContain("focus:1");
    }

    [Fact]
    public void Detach_all_restores_hidden_windows_first_and_the_active_one_last()
    {
        var (alfa, beta) = AttachTwo();
        var emptied = false;
        _session.Emptied += (_, _) => emptied = true;
        _journal.Clear();

        _session.DetachAll();

        _journal.Where(e => e.StartsWith("restore", StringComparison.Ordinal)).ShouldBe(["restore:2", "restore:1"]);
        alfa.Bounds.ShouldBe(s_original);
        alfa.Visible.ShouldBeTrue();
        beta.Visible.ShouldBeTrue();
        _session.Windows.ShouldBeEmpty();
        _store.Current.Windows.ShouldBeEmpty();
        _store.Current.CleanShutdown.ShouldBeTrue();
        emptied.ShouldBeTrue();
    }

    [Fact]
    public void Detaching_at_a_drop_point_restores_then_places_the_window_there()
    {
        var (_, beta) = AttachTwo();
        var dropped = new PixelRect(900, 500, 1700, 1100);
        _journal.Clear();

        _session.Detach(_session.Windows[1].Id, dropped).IsSuccess.ShouldBeTrue();

        _journal.IndexOf("restore:2").ShouldBeLessThan(_journal.IndexOf("place:2"));
        beta.Bounds.ShouldBe(dropped);
        _session.Windows.Count.ShouldBe(1);
        _store.Current.Windows.Count.ShouldBe(1);
    }

    [Fact]
    public void Health_check_removes_windows_that_disappeared_without_an_event()
    {
        var (_, beta) = AttachTwo();

        beta.Alive = false;
        _session.CheckHealth();

        _session.Windows.Count.ShouldBe(1);
        _store.Current.Windows.Count.ShouldBe(1);
    }

    [Fact]
    public void Health_check_flags_and_clears_unresponsive_windows()
    {
        var (alfa, _) = AttachTwo();
        var window = _session.Windows[0];

        alfa.Hung = true;
        _session.CheckHealth();
        window.State.ShouldBe(ManagedWindowState.Unresponsive);

        alfa.Hung = false;
        _session.CheckHealth();
        window.State.ShouldBe(ManagedWindowState.Attached);
    }

    [Fact]
    public void Requesting_close_keeps_the_tab_until_the_window_is_destroyed()
    {
        var (alfa, _) = AttachTwo();
        var window = _session.Windows[0];

        _session.RequestClose(window.Id).IsSuccess.ShouldBeTrue();

        _journal.ShouldContain("close:1");
        window.State.ShouldBe(ManagedWindowState.Closing);
        _session.Windows.ShouldContain(window);

        alfa.Alive = false;
        _session.HandleEvent(new WindowEvent(WindowEventKind.Destroyed, alfa.Handle));
        _session.Windows.ShouldNotContain(window);
    }

    [Fact]
    public void Title_changes_are_published()
    {
        var (alfa, _) = AttachTwo();
        var updated = new List<string>();
        _session.WindowUpdated += (_, e) => updated.Add(e.Window.Title);

        alfa.Title = "● index.ts - proyecto-alfa";
        _session.HandleEvent(new WindowEvent(WindowEventKind.NameChanged, alfa.Handle));

        updated.ShouldBe(["● index.ts - proyecto-alfa"]);
    }

    [Fact]
    public void Events_for_unmanaged_windows_are_ignored()
    {
        AttachTwo();
        _journal.Clear();

        _session.HandleEvent(new WindowEvent(WindowEventKind.Destroyed, new WindowHandle(777)));

        _journal.ShouldBeEmpty();
        _session.Windows.Count.ShouldBe(2);
    }

    private (FakeWindow Alfa, FakeWindow Beta) AttachTwo()
    {
        var alfa = _windows.Add(1, "proyecto-alfa", s_original);
        var beta = _windows.Add(2, "proyecto-beta", s_original);
        _session.Attach(_windows.Info(alfa));
        _session.Attach(_windows.Info(beta));
        return (alfa, beta);
    }
}
