using Microsoft.Extensions.Time.Testing;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;
using TabDock.Core.Services;
using TabDock.Core.Tests.Fakes;

namespace TabDock.Core.Tests.Services;

public sealed class RulesTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_settings_enable_only_the_vs_code_rule()
    {
        var settings = SettingsDocument.CreateDefault(s_now);

        settings.Rules.Where(r => r.Enabled).Select(r => r.ExecutableName).ShouldBe(["Code.exe"]);
        settings.Rules.Select(r => r.ExecutableName).ShouldBe(["Code.exe", "Code - Insiders.exe", "VSCodium.exe", "Cursor.exe"]);
        settings.Rules.ShouldAllBe(r => r.TitleParser == TitleParserKind.VsCode && r.AutoMerge && !r.MergeOnStartup);
        settings.Hotkeys.Count.ShouldBe(15);
    }

    [Fact]
    public void Matcher_compares_executable_names_case_insensitively_and_checks_the_class()
    {
        var matcher = new RuleMatcher(SettingsDocument.CreateDefault(s_now).Rules);

        matcher.Match(Window("code.EXE", "Chrome_WidgetWin_1"))!.ExecutableName.ShouldBe("Code.exe");
        matcher.Match(Window("Code.exe", "OtraClase")).ShouldBeNull();
        matcher.Match(Window("notepad.exe", "Notepad")).ShouldBeNull();
    }

    [Fact]
    public void Disabled_rules_never_match()
    {
        var matcher = new RuleMatcher(SettingsDocument.CreateDefault(s_now).Rules);

        matcher.Match(Window("Cursor.exe", "Chrome_WidgetWin_1")).ShouldBeNull();
    }

    [Theory]
    [InlineData(@"*\Programs\Microsoft VS Code\Code.exe", true)]
    [InlineData(@"D:\*\Code.exe", false)]
    [InlineData(@"*\programs\microsoft vs code\code.exe", true)]
    [InlineData(@"*\Programs\Microsoft VS Code?Code.exe", true)]
    public void Path_pattern_supports_wildcards(string pattern, bool expected)
    {
        var rule = SettingsDocument.CreateDefault(s_now).Rules[0] with { ExecutablePathPattern = pattern };

        var match = new RuleMatcher([rule]).Match(Window("Code.exe", "Chrome_WidgetWin_1"));

        (match is not null).ShouldBe(expected);
    }

    [Fact]
    public void Path_pattern_treats_regex_characters_literally()
    {
        var rule = SettingsDocument.CreateDefault(s_now).Rules[0] with { ExecutablePathPattern = @"C:\Programas (x86)\[Editor]+\Code.exe" };
        var window = Window("Code.exe", "Chrome_WidgetWin_1") with { ExecutablePath = @"C:\Programas (x86)\[Editor]+\Code.exe" };

        new RuleMatcher([rule]).Match(window).ShouldNotBeNull();
    }

    [Fact]
    public void Grouping_finds_only_eligible_windows_of_the_rule()
    {
        var journal = new List<string>();
        var windows = new FakeSystemWithInfo(journal);
        var rule = SettingsDocument.CreateDefault(s_now).Rules[0];
        windows.Add(Window("Code.exe", "Chrome_WidgetWin_1", 1));
        windows.Add(Window("Code.exe", "Chrome_WidgetWin_1", 2) with { HasOwner = true });
        windows.Add(Window("notepad.exe", "Notepad", 3));
        var service = new GroupingService(windows, new EligibilityPolicy(new EligibilityOptions(), ownProcessId: 1), () => new RuleMatcher([rule]));

        service.FindMergeCandidates(rule).Select(w => (int)w.Handle.Value).ShouldBe([1]);
        service.RuleFor(new WindowHandle(1))!.Id.ShouldBe(rule.Id);
        service.RuleFor(new WindowHandle(3)).ShouldBeNull();
        service.EvaluateNewWindow(new WindowHandle(1))!.Value.Rule.Id.ShouldBe(rule.Id);
        service.EvaluateNewWindow(new WindowHandle(2)).ShouldBeNull();
    }

    [Fact]
    public void Elevated_windows_of_the_rule_are_counted_as_blocked()
    {
        var journal = new List<string>();
        var windows = new FakeSystemWithInfo(journal);
        var rule = SettingsDocument.CreateDefault(s_now).Rules[0];
        windows.Add(Window("Code.exe", "Chrome_WidgetWin_1", 1));
        windows.Add(Window("Code.exe", "Chrome_WidgetWin_1", 2) with { IsElevatedProcess = true });
        windows.Add(Window("notepad.exe", "Notepad", 3) with { IsElevatedProcess = true });
        var service = new GroupingService(windows, new EligibilityPolicy(new EligibilityOptions(), ownProcessId: 1), () => new RuleMatcher([rule]));

        service.CountBlocked(rule).ShouldBe(1);
        service.FindMergeCandidates(rule).Count.ShouldBe(1);
    }

    [Fact]
    public void Auto_merge_needs_the_rule_to_allow_it()
    {
        var journal = new List<string>();
        var windows = new FakeSystemWithInfo(journal);
        var rule = SettingsDocument.CreateDefault(s_now).Rules[0] with { AutoMerge = false };
        windows.Add(Window("Code.exe", "Chrome_WidgetWin_1", 1));
        var service = new GroupingService(windows, new EligibilityPolicy(new EligibilityOptions(), ownProcessId: 1), () => new RuleMatcher([rule]));

        service.EvaluateNewWindow(new WindowHandle(1)).ShouldBeNull();
    }

    [Fact]
    public void Debouncer_waits_for_quiet_period_and_restarts_on_new_events()
    {
        var time = new FakeTimeProvider();
        var debouncer = new AutoMergeDebouncer(time);
        var handle = new WindowHandle(1);

        debouncer.Notify(handle);
        time.Advance(TimeSpan.FromMilliseconds(100));
        debouncer.TakeDue().ShouldBeEmpty();

        debouncer.Notify(handle);
        time.Advance(TimeSpan.FromMilliseconds(100));
        debouncer.TakeDue().ShouldBeEmpty();

        time.Advance(TimeSpan.FromMilliseconds(60));
        debouncer.TakeDue().ShouldBe([handle]);
        debouncer.HasPending.ShouldBeFalse();
    }

    [Fact]
    public void Debouncer_forgets_destroyed_windows()
    {
        var time = new FakeTimeProvider();
        var debouncer = new AutoMergeDebouncer(time);

        debouncer.Notify(new WindowHandle(1));
        debouncer.Forget(new WindowHandle(1));
        time.Advance(TimeSpan.FromSeconds(1));

        debouncer.TakeDue().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(HotkeyCommand.MergeAll, "Ctrl+Alt+M")]
    [InlineData(HotkeyCommand.NextTab, "Ctrl+Alt+PageDown")]
    [InlineData(HotkeyCommand.PreviousTab, "Ctrl+Alt+PageUp")]
    [InlineData(HotkeyCommand.GoToTab3, "Ctrl+Alt+3")]
    public void Hotkeys_have_readable_names(HotkeyCommand command, string expected)
    {
        HotkeyBinding.Defaults.Single(b => b.Command == command).ToDisplayString().ShouldBe(expected);
    }

    private static WindowInfo Window(string executable, string className, int handle = 1) => new()
    {
        Handle = new WindowHandle(handle),
        ProcessId = 2000,
        ExecutablePath = @"C:\Usuarios\usuario-ficticio\AppData\Local\Programs\Microsoft VS Code\" + executable,
        ClassName = className,
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

    /// <summary>Doble mínimo que devuelve exactamente la información dada.</summary>
    private sealed class FakeSystemWithInfo(List<string> journal) : IWindowSystem
    {
        private readonly List<WindowInfo> _windows = [];

        public void Add(WindowInfo info) => _windows.Add(info);

        public IReadOnlyList<WindowInfo> EnumerateTopLevel() => _windows;

        public Result<WindowInfo> GetInfo(WindowHandle handle) =>
            _windows.Find(w => w.Handle == handle) is { } w ? Result.Success(w) : Result.Fail<WindowInfo>(new DomainError("window.notFound", "x"));

        public bool IsAlive(WindowHandle handle) => true;

        public bool IsHung(WindowHandle handle) => false;

        public bool IsVisible(WindowHandle handle) => true;

        public int GetCaptionInset(WindowHandle handle) => 0;

        public bool Matches(WindowIdentity identity) => true;

        public Result<WindowSnapshot> CaptureSnapshot(WindowHandle handle) => throw new NotSupportedException();

        public Result Restore(WindowSnapshot snapshot) => throw new NotSupportedException();

        public Result Show(WindowHandle handle) => Log("show");

        public Result Hide(WindowHandle handle) => Log("hide");

        public Result PlaceInSlot(WindowHandle handle, PixelRect slot) => Log("place");

        public Result PlaceAbove(WindowHandle window, WindowHandle below) => Log("above");

        public bool IsForeground(WindowHandle handle) => false;

        public Result BringToForeground(WindowHandle handle) => Log("focus");

        public Result RequestClose(WindowHandle handle) => Log("close");

        public PixelRect GetWorkArea(WindowHandle handle) => new(0, 0, 1920, 1040);

        public Result SetFrameShadow(WindowHandle handle, bool visible) => Log("shadow");

        public bool IsAbove(WindowHandle upper, WindowHandle lower) => true;

        public Result SetTaskbarButton(WindowHandle handle, bool visible) => Log("taskbar");

        private Result Log(string operation)
        {
            journal.Add(operation);
            return Result.Success();
        }
    }
}
