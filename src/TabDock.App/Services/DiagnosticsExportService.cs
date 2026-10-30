using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using TabDock.Core;
using TabDock.Core.Services;
using TabDock.Infrastructure.Diagnostics;
using TabDock.Win32.Shell;

namespace TabDock.App.Services;

/// <summary>Reúne los datos del equipo y exporta el ZIP de diagnóstico (Ajustes → Diagnóstico o <c>--export-diagnostics</c>).</summary>
public sealed class DiagnosticsExportService(DiagnosticsExporter exporter, SettingsService settings)
{
    public Result Export(string zipPath)
    {
        var environment = new DiagnosticsEnvironment(
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?",
            Environment.OSVersion.Version.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            CultureInfo.CurrentUICulture.Name,
            Displays.Enumerate());

        return exporter.Export(zipPath, settings.Current, environment, DiagnosticsScrubber.FromEnvironment());
    }
}
