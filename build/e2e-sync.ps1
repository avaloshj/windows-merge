<#
.SYNOPSIS
    Prueba de extremo a extremo de la Fase 3. Sincronización marco/ventana activa: pestañas, mover, minimizar y cierres externos.
.DESCRIPTION
    Abre ventanas TabDock.TestTarget con títulos ficticios. Requiere un escritorio interactivo y compilar antes
    la solución (dotnet build). Cierra todas las instancias de TabDock y TestTarget al empezar y al terminar.
#>
param([string] $Root = (Split-Path -Parent $PSScriptRoot))
# Sincronización marco ↔ ventana activa con TestTarget (títulos ficticios). No captura pantalla.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Sync {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
}
"@
[Sync]::SetProcessDpiAwarenessContext([IntPtr]-4) | Out-Null

$exe = "$Root\src\TabDock.App\bin\Debug\net10.0-windows\TabDock.exe"
$target = "$Root\src\TabDock.TestTarget\bin\Debug\net10.0-windows\TabDock.TestTarget.exe"
$script:failures = 0
function Check([string] $name, [bool] $ok, [string] $detail = '') { if ($ok) { "  OK    $name $detail" } else { "  FALLO $name $detail"; $script:failures++ } }
function Ext([IntPtr] $h) { $r = New-Object Sync+RECT; [Sync]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) | Out-Null; $r }
function Win([IntPtr] $h) { $r = New-Object Sync+RECT; [Sync]::GetWindowRect($h, [ref]$r) | Out-Null; $r }
function VisibleTargets { @($script:targets | Where-Object { -not $_.HasExited -and [Sync]::IsWindowVisible($_.MainWindowHandle) }) }
# Derecha y abajo exactos; arriba, dentro del marco: la barra nativa de TestTarget queda bajo la del marco (ADR 0011).
function Fits($frameH, $activeH) { $f = Win $frameH; $a = Ext $activeH; ($a.R -eq $f.R) -and ($a.B -eq $f.B) -and ($a.T -ge $f.T) -and ($a.T -lt $f.T + 80) -and ($a.L -gt $f.L) }
function Wait-Visible($processes) {
    # Espera (máx. 15 s) a que todas las ventanas de prueba sean visibles: en un equipo cargado el arranque varía.
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 250
        foreach ($p in $processes) { $p.Refresh() }
    } while (@($processes | Where-Object { $_.MainWindowHandle -eq 0 }).Count -gt 0 -and [DateTime]::UtcNow -lt $deadline)
    Start-Sleep -Milliseconds 500
}
function Tabs($frame) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    @($frame.FindAll('Descendants', $cond))
}

. "$PSScriptRoot\e2e-common.ps1"
Assert-SafeToTest "$env:APPDATA\TabDock"
Get-Process TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force
Backup-UserFiles "$env:APPDATA\TabDock"
try {
    $titles = 'proyecto-alfa - TestTarget', 'proyecto-beta - TestTarget', 'demo-api - TestTarget'
    $script:targets = $titles | ForEach-Object { Start-Process $target -ArgumentList "--title `"$_`"" -PassThru }
    Wait-Visible $script:targets

    $main = Start-Process $exe -ArgumentList '--merge-exe', 'TabDock.TestTarget.exe' -PassThru
    Start-Sleep 5; $main.Refresh()
    $fh = $main.MainWindowHandle
    $frame = [System.Windows.Automation.AutomationElement]::FromHandle($fh)
    $script:scale = (Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);' -Name Dpi -Namespace X -PassThru)::GetDpiForWindow($fh) / 96

    "1. Pestañas y ventana activa"
    Check 'tres pestañas en la barra lateral' ((Tabs $frame).Count -eq 3) "($((Tabs $frame).Count))"
    $active = VisibleTargets
    Check 'una ventana visible y ajustada al hueco' (($active.Count -eq 1) -and (Fits $fh $active[0].MainWindowHandle))

    "2. Cambio de pestaña"
    $tab = Tabs $frame | Where-Object { $_.Current.Name -like 'demo-api*' } | Select-Object -First 1
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $demo = $script:targets[2]
    while (-not [Sync]::IsWindowVisible($demo.MainWindowHandle) -and $sw.ElapsedMilliseconds -lt 2000) { Start-Sleep -Milliseconds 5 }
    $latency = $sw.ElapsedMilliseconds
    Start-Sleep -Milliseconds 300
    $active = VisibleTargets
    Check 'solo demo-api visible tras seleccionarla' (($active.Count -eq 1) -and ($active[0].Id -eq $demo.Id)) "(latencia hasta visible ≈ $latency ms, incluye UI Automation)"
    Check 'demo-api ajustada al hueco' (Fits $fh $demo.MainWindowHandle)

    "3. Mover y redimensionar el marco"
    $f = Win $fh
    [Sync]::SetWindowPos($fh, [IntPtr]::Zero, $f.L + 60, $f.T + 40, ($f.R - $f.L) - 120, ($f.B - $f.T) - 80, 0x14) | Out-Null   # NOZORDER|NOACTIVATE
    Start-Sleep -Milliseconds 700
    Check 'la ventana activa sigue al marco movido y redimensionado' (Fits $fh $demo.MainWindowHandle) "(marco $((Win $fh).L),$((Win $fh).T),$((Win $fh).R),$((Win $fh).B) · activa $((Ext $demo.MainWindowHandle).L),$((Ext $demo.MainWindowHandle).T),$((Ext $demo.MainWindowHandle).R),$((Ext $demo.MainWindowHandle).B))"

    "4. Minimizar y restaurar el marco"
    [Sync]::ShowWindow($fh, 6) | Out-Null   # SW_MINIMIZE
    Start-Sleep -Milliseconds 700
    Check 'ninguna ventana visible con el marco minimizado' ((VisibleTargets).Count -eq 0)
    [Sync]::ShowWindow($fh, 9) | Out-Null   # SW_RESTORE
    Start-Sleep -Milliseconds 900
    $active = VisibleTargets
    Check 'la activa vuelve al restaurar el marco' (($active.Count -eq 1) -and ($active[0].Id -eq $demo.Id) -and (Fits $fh $demo.MainWindowHandle))

    "5. Ventanas que se cierran solas"
    $script:targets[0] | Stop-Process -Force
    Start-Sleep -Milliseconds 2600
    Check 'la pestaña de una ventana oculta cerrada desaparece' ((Tabs $frame).Count -eq 2) "($((Tabs $frame).Count))"
    $demo | Stop-Process -Force
    Start-Sleep -Milliseconds 2600
    $active = VisibleTargets
    Check 'al cerrarse la activa se muestra la siguiente' (($active.Count -eq 1) -and ($active[0].Id -eq $script:targets[1].Id))
    Check 'queda una pestaña' ((Tabs $frame).Count -eq 1)

    "6. Último cierre: el marco se cierra solo"
    $script:targets[1] | Stop-Process -Force
    Start-Sleep -Seconds 3
    Check 'TabDock termina al quedarse sin ventanas' (@(Get-Process TabDock -ErrorAction SilentlyContinue).Count -eq 0)

    Get-Process TabDock, TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force
}
finally {
    # Pase lo que pase, no deja TabDock ni ventanas de prueba abiertas.
    Get-Process TabDock, TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Restore-UserFiles "$env:APPDATA\TabDock"
}

""
if ($script:failures -eq 0) { "RESULTADO: todo correcto" } else { "RESULTADO: $script:failures fallos" }
