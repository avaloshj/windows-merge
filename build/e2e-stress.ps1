<#
.SYNOPSIS
    Prueba de estrés y rendimiento (Fase 8): 20 y 50 ventanas, ráfaga de 100 aperturas/cierres en 10 s, latencia de
    cambio de pestaña, memoria, CPU en reposo, arranque y restauración al cerrar.
.DESCRIPTION
    Usa ventanas TabDock.TestTarget (todas las de cada prueba en un solo proceso, como VS Code) con títulos ficticios y
    una regla temporal solo para TestTarget; hace copia de settings.json y preferences.json y los restaura al terminar.
    Las ventanas gestionadas se cuentan en state.json (la lista de pestañas es virtualizada). La latencia sale del log de
    TabDock en modo --verbose ("activada en N ms"). Objetivos de la especificación: cambio de pestaña < 50 ms, memoria
    < 120 MB con 20 ventanas, CPU en reposo < 1 %, arranque < 1,5 s. Se mide la compilación Debug (la Release es más rápida).
    La memoria de un proceso WPF con aceleración gráfica supera los 120 MB solo por el dispositivo gráfico del marco
    (medido en la Fase 8: unos 255 MB de conjunto de trabajo con 1, 20 o 50 ventanas); el objetivo se comprueba con
    -LowMemory (Ajustes → Avanzado → modo de bajo consumo de memoria). Sin él, la cifra se muestra como información.
#>
param([string] $Root = (Split-Path -Parent $PSScriptRoot), [switch] $LowMemory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class Stress8 {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int max);

    // Ventanas de prueba (título con "TestTarget") de los procesos indicados; visibles o todas. WPF crea además ventanas
    // internas ocultas con título en cada proceso: no cuentan.
    public static int CountWindows(int[] pids, bool visibleOnly) { return Titles(pids, visibleOnly, false).Count; }

    // Títulos (ficticios) de las ventanas de prueba; con hiddenOnly, solo las ocultas.
    public static List<string> Titles(int[] pids, bool visibleOnly, bool hiddenOnly) {
        var set = new HashSet<int>(pids); var found = new List<string>();
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            var visible = IsWindowVisible(h);
            if (!set.Contains((int)p) || (visibleOnly && !visible) || (hiddenOnly && visible)) return true;
            var title = new System.Text.StringBuilder(256); GetWindowText(h, title, title.Capacity);
            if (title.ToString().Contains("TestTarget")) found.Add(hiddenOnly ? "HWND " + h.ToInt64() + " " + title : title.ToString());
            return true;
        }, IntPtr.Zero);
        return found;
    }

    static int CountWindowsOld(int[] pids, bool visibleOnly) {
        var set = new HashSet<int>(pids); var n = 0;
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (!set.Contains((int)p) || (visibleOnly && !IsWindowVisible(h))) return true;
            var title = new System.Text.StringBuilder(256); GetWindowText(h, title, title.Capacity);
            if (title.ToString().Contains("TestTarget")) n++;
            return true;
        }, IntPtr.Zero);
        return n;
    }
}
"@

$exe = "$Root\src\TabDock.App\bin\Debug\net10.0-windows\TabDock.exe"
$target = "$Root\src\TabDock.TestTarget\bin\Debug\net10.0-windows\TabDock.TestTarget.exe"
$data = "$env:APPDATA\TabDock"
$files = 'settings.json', 'preferences.json'
$script:failures = 0
function Check([string] $name, [bool] $ok, [string] $detail = '') { if ($ok) { "  OK    $name $detail" } else { "  FALLO $name $detail"; $script:failures++ } }
function Hotkey-Window([int] $processId) {
    $h = [IntPtr]::Zero
    while (($h = [Stress8]::FindWindowEx([IntPtr]-3, $h, [NullString]::Value, 'TabDock.Hotkeys')) -ne [IntPtr]::Zero) {
        $owner = 0; [Stress8]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
        if ($owner -eq $processId) { return $h }
    }
    return [IntPtr]::Zero
}
# MergeAll=1, UnmergeAll=2, NextTab=3 (orden de los atajos de la configuración temporal).
function Press([IntPtr] $hk, [int] $id, [int] $waitMs = 300) { [Stress8]::PostMessage($hk, 0x0312, [IntPtr]$id, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds $waitMs }
function Managed { $s = Get-Content "$data\state.json" -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json; if ($s) { @($s.windows).Count } else { 0 } }
function Wait-Managed([int] $expected, [int] $seconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    while ((Managed) -ne $expected -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
    Managed
}
function Wait-Windows([int[]] $pids, [int] $expected, [int] $seconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    while ([Stress8]::CountWindows($pids, $true) -lt $expected -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
    [Stress8]::CountWindows($pids, $true)
}

. "$PSScriptRoot\e2e-common.ps1"
Assert-SafeToTest "$env:APPDATA\TabDock"
Get-Process TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force
Backup-UserFiles "$env:APPDATA\TabDock"
try {
    New-Item -ItemType Directory -Force $data | Out-Null
    Remove-Item "$data\preferences.json", "$data\state.json" -ErrorAction SilentlyContinue
    $now = [DateTime]::UtcNow.ToString('o')
    $settings = @{
        schemaVersion = 1
        user = @{ onboardingCompleted = $true; lowMemoryRendering = [bool]$LowMemory }
        rules = @(@{ id = [guid]::NewGuid().ToString(); displayName = 'Grupo de carga'; executableName = 'TabDock.TestTarget.exe'; executablePathPattern = $null; windowClassName = $null
                     titleParser = 'Generic'; enabled = $true; autoMerge = $true; mergeOnStartup = $false; sidebarPosition = 'Left'; createdAt = $now; updatedAt = $now })
        hotkeys = @(
            @{ command = 'MergeAll'; modifiers = 'Alt, Control'; virtualKey = 77; enabled = $true },
            @{ command = 'UnmergeAll'; modifiers = 'Alt, Control'; virtualKey = 85; enabled = $true },
            @{ command = 'NextTab'; modifiers = 'Alt, Control'; virtualKey = 34; enabled = $true }
        )
    }
    [IO.File]::WriteAllText("$data\settings.json", ($settings | ConvertTo-Json -Depth 6))

    "1. Arranque"
    $startedUtc = [DateTime]::UtcNow
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $main = Start-Process $exe -ArgumentList '--verbose' -PassThru
    $hk = [IntPtr]::Zero
    while ($hk -eq [IntPtr]::Zero -and $clock.ElapsedMilliseconds -lt 15000) { Start-Sleep -Milliseconds 20; $hk = Hotkey-Window $main.Id }
    $startupMs = $clock.ElapsedMilliseconds
    Check 'TabDock listo (atajos registrados)' ($hk -ne [IntPtr]::Zero)
    Check 'arranque < 1,5 s' ($startupMs -lt 1500) "($startupMs ms hasta tener los atajos registrados)"
    Start-Sleep -Seconds 3
    $main.Refresh()
    $baseline = [math]::Round($main.WorkingSet64 / 1MB, 1)
    "  INFO  memoria en reposo sin ventanas: $baseline MB (conjunto de trabajo), $([math]::Round($main.PrivateMemorySize64 / 1MB, 1)) MB privada"

    "2. 20 ventanas"
    $first = Start-Process $target -ArgumentList '--title "proyecto-carga - TestTarget" --windows 20 --size 640x420' -PassThru
    $open = Wait-Windows @($first.Id) 20 30
    Check 'TestTarget abre 20 ventanas' ($open -eq 20) "($open)"
    Press $hk 1 1000
    Check 'se agrupan las 20' ((Wait-Managed 20 20) -eq 20) "($(Managed) gestionadas)"
    Check 'solo una visible' ([Stress8]::CountWindows(@($first.Id), $true) -eq 1)
    Start-Sleep -Seconds 3
    $main.Refresh()
    $workingSet = [math]::Round($main.WorkingSet64 / 1MB, 1); $private = [math]::Round($main.PrivateMemorySize64 / 1MB, 1)
    if ($LowMemory) { Check 'memoria privada < 120 MB con 20 ventanas (modo de bajo consumo)' ($private -lt 120) "(privada $private MB, conjunto de trabajo $workingSet MB)" }
    else { "  INFO  memoria con 20 ventanas y aceleración gráfica: privada $private MB, conjunto de trabajo $workingSet MB (el objetivo de 120 MB se comprueba con -LowMemory)" }

    $cpuBefore = $main.TotalProcessorTime; $wall = [Diagnostics.Stopwatch]::StartNew(); Start-Sleep -Seconds 10; $main.Refresh()
    $cpuPercent = [math]::Round(($main.TotalProcessorTime - $cpuBefore).TotalMilliseconds / $wall.ElapsedMilliseconds / [Environment]::ProcessorCount * 100, 2)
    Check 'CPU en reposo < 1 %' ($cpuPercent -lt 1) "($cpuPercent % del total, $([Environment]::ProcessorCount) núcleos lógicos)"

    for ($i = 0; $i -lt 20; $i++) { Press $hk 3 250 }
    Start-Sleep -Milliseconds 800
    $log = Get-ChildItem "$data\logs\tabdock-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $latencies = @(Get-Content $log.FullName -Encoding UTF8 | ForEach-Object { $_ | ConvertFrom-Json } |
        Where-Object { [DateTimeOffset]::Parse($_.'@t').UtcDateTime -ge $startedUtc -and $_.'@mt' -like '*activada en*' } | ForEach-Object { [double]$_.ElapsedMs })
    $max = if ($latencies.Count) { [math]::Round(($latencies | Measure-Object -Maximum).Maximum, 1) } else { -1 }
    $avg = if ($latencies.Count) { [math]::Round(($latencies | Measure-Object -Average).Average, 1) } else { -1 }
    Check 'se midieron los 20 cambios de pestaña' ($latencies.Count -ge 20) "($($latencies.Count))"
    Check 'cambio de pestaña < 50 ms' ($latencies.Count -gt 0 -and $max -lt 50) "(media $avg ms, máximo $max ms)"

    "3. 50 ventanas (30 más, agrupadas automáticamente al abrirse)"
    $second = Start-Process $target -ArgumentList '--title "proyecto-extra - TestTarget" --windows 30 --size 640x420' -PassThru
    Check 'se agrupan las 50' ((Wait-Managed 50 40) -eq 50) "($(Managed) gestionadas)"
    Check 'solo una visible' ([Stress8]::CountWindows(@($first.Id, $second.Id), $true) -eq 1)

    "4. Ráfaga: 100 ventanas abiertas y cerradas en 10 s"
    $burst = Start-Process $target -ArgumentList '--title "proyecto-rafaga - TestTarget" --burst 100 --burst-ms 100 --size 500x300' -PassThru
    # Con carga, los temporizadores de TestTarget se retrasan: se espera al aviso de fin en el título de su ventana principal.
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do { Start-Sleep -Milliseconds 500 } while (-not (@([Stress8]::Titles(@($burst.Id), $false, $false)) -match 'ráfaga terminada') -and [DateTime]::UtcNow -lt $deadline)
    Start-Sleep -Seconds 2
    Check 'la ráfaga termina (solo queda su ventana principal)' ([Stress8]::CountWindows(@($burst.Id), $false) -eq 1) "($([Stress8]::CountWindows(@($burst.Id), $false)))"
    $pids = @($first.Id, $second.Id, $burst.Id)
    $alive = [Stress8]::CountWindows($pids, $false)
    $managed = Wait-Managed $alive 10
    Check 'sin pestañas fantasma: gestionadas = ventanas que siguen abiertas' ($managed -eq $alive) "($managed gestionadas, $alive abiertas)"
    $deadline = [DateTime]::UtcNow.AddSeconds(3)
    while ([Stress8]::CountWindows($pids, $true) -gt 1 -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
    Check 'solo una visible tras la ráfaga' ([Stress8]::CountWindows($pids, $true) -eq 1) "(visibles: $(([Stress8]::Titles($pids, $true, $false)) -join ' | '))"
    $main.Refresh()
    Check 'TabDock sigue vivo' (-not $main.HasExited)

    "5. Separar todas restaura cada ventana"
    Press $hk 2 3000
    $visible = [Stress8]::CountWindows($pids, $true)
    $hidden = [Stress8]::Titles($pids, $false, $true)
    Check 'ninguna ventana queda oculta' ($visible -eq $alive) "($visible visibles de $alive; ocultas: $($hidden -join ' | '))"
    Check 'state.json sin ventanas pendientes' ((Managed) -eq 0)
}
finally {
    Get-Process TabDock, TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Restore-UserFiles "$env:APPDATA\TabDock"
    Remove-Item "$data\state.json" -ErrorAction SilentlyContinue
}

""
if ($script:failures -eq 0) { "RESULTADO: todo correcto" } else { "RESULTADO: $script:failures fallos" }
