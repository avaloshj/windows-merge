using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TabDock.App.Localization;
using TabDock.Core.Abstractions;
using TabDock.Core.Diagnostics;
using TabDock.Core.Services;
using TabDock.Infrastructure;

namespace TabDock.App.ViewModels;

/// <summary>Lista las ventanas de primer nivel con su ejecutable, clase y motivo de exclusión.</summary>
public sealed partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IWindowSystem _windowSystem;
    private readonly EligibilityPolicy _policy;
    private readonly AppPaths _paths;
    private readonly ILogger<DiagnosticsViewModel> _logger;

    private IReadOnlyList<EvaluatedWindow> _snapshot = [];

    public DiagnosticsViewModel(IWindowSystem windowSystem, EligibilityPolicy policy, AppPaths paths, ILogger<DiagnosticsViewModel> logger)
    {
        _windowSystem = windowSystem;
        _policy = policy;
        _paths = paths;
        _logger = logger;
        Summary = string.Empty;
    }

    public ObservableCollection<WindowRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial bool OnlyEligible { get; set; }

    /// <summary>Por defecto los títulos se muestran enmascarados: pueden contener datos personales.</summary>
    [ObservableProperty]
    public partial bool ShowTitles { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [RelayCommand]
    private void Refresh()
    {
        var stopwatch = Stopwatch.StartNew();
        _snapshot = [.. _windowSystem.EnumerateTopLevel().Select(w => new EvaluatedWindow(w, _policy.Evaluate(w)))];
        stopwatch.Stop();

        var eligible = _snapshot.Count(e => e.Result.Status == EligibilityStatus.Eligible);
        var disabled = _snapshot.Count(e => e.Result.Status == EligibilityStatus.Disabled);
        Summary = TextResources.Format("Diag_Summary", _snapshot.Count, eligible, disabled);

        LogEnumerated(_snapshot.Count, eligible, disabled, stopwatch.ElapsedMilliseconds);
        foreach (var item in _snapshot.Where(e => e.Result.Status != EligibilityStatus.Excluded))
            LogCandidate(item.Result.Status, item.Result.Reason, item.Window);

        ApplyFilter();
    }

    [RelayCommand]
    private void OpenLogs()
    {
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        startInfo.ArgumentList.Add(_paths.Logs);
        using var _ = Process.Start(startInfo);
    }

    partial void OnOnlyEligibleChanged(bool value) => ApplyFilter();

    partial void OnShowTitlesChanged(bool value) => ApplyFilter();

    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var item in _snapshot)
        {
            if (!OnlyEligible || item.Result.IsEligible)
                Rows.Add(ToRow(item));
        }

        IsEmpty = Rows.Count == 0;
    }

    private WindowRowViewModel ToRow(EvaluatedWindow item)
    {
        var (window, result) = item;
        var bounds = window.ExtendedFrameBounds;
        return new WindowRowViewModel(
            RawHandle: window.Handle,
            StatusKind: result.Status,
            Status: TextResources.Get("Status_" + result.Status.ToString()),
            Reason: TextResources.Get("Reason_" + result.Reason.ToString()),
            Executable: string.IsNullOrEmpty(window.ExecutableName) ? TextResources.Get("Diag_Unknown") : window.ExecutableName,
            ClassName: window.ClassName,
            ProcessId: window.ProcessId,
            Size: window.ShowState == WindowShowState.Minimized
                ? "—"
                : string.Create(CultureInfo.InvariantCulture, $"{bounds.Width}×{bounds.Height}"),
            Title: ShowTitles ? window.Title : LogMask.Hash(window.Title),
            Handle: window.Handle.ToString());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Diagnóstico: {Total} ventanas, {Eligible} elegibles, {Disabled} deshabilitadas en {ElapsedMs} ms")]
    private partial void LogEnumerated(int total, int eligible, int disabled, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Candidata {Status} ({Reason}): {@Window}")]
    private partial void LogCandidate(EligibilityStatus status, ExclusionReason reason, WindowInfo window);

    private readonly record struct EvaluatedWindow(WindowInfo Window, EligibilityResult Result);
}
