<#
.SYNOPSIS
    Prueba de extremo a extremo de la Fase 3. Garantía de restauración: guardián, cierre normal y recuperación al arrancar.
.DESCRIPTION
    Abre ventanas TabDock.TestTarget con títulos ficticios. Requiere un escritorio interactivo y compilar antes
    la solución (dotnet build). Cierra todas las instancias de TabDock y TestTarget al empezar y al terminar.
#>
param([string] $Root = (Split-Path -Parent $PSScriptRoot))
# Prueba de extremo a extremo de la Fase 3 con ventanas TestTarget (títulos ficticios). No captura pantalla.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class E2E {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
}
"@
[E2E]::SetProcessDpiAwarenessContext([IntPtr]-4) | Out-Null   # PER_MONITOR_AWARE_V2: mismas coordenadas que TabDock

$exe = "$Root\src\TabDock.App\bin\Debug\net10.0-windows\TabDock.exe"
$target = "$Root\src\TabDock.TestTarget\bin\Debug\net10.0-windows\TabDock.TestTarget.exe"
$state = "$env:APPDATA\TabDock\state.json"
$script:failures = 0

function Check([string] $name, [bool] $ok, [string] $detail = '') {
    if ($ok) { "  OK    $name $detail" } else { "  FALLO $name $detail"; $script:failures++ }
}
function Frame([IntPtr] $h) { $r = New-Object E2E+RECT; [E2E]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) | Out-Null; "$($r.L),$($r.T),$($r.R),$($r.B)" }
function Rect([IntPtr] $h) { $r = New-Object E2E+RECT; [E2E]::GetWindowRect($h, [ref]$r) | Out-Null; $r }

function Wait-Visible($processes) {
    # Espera (máx. 15 s) a que todas las ventanas de prueba sean visibles: en un equipo cargado el arranque varía.
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 250
        foreach ($p in $processes) { $p.Refresh() }
    } while (@($processes | Where-Object { $_.MainWindowHandle -eq 0 }).Count -gt 0 -and [DateTime]::UtcNow -lt $deadline)
    Start-Sleep -Milliseconds 500
}
function Start-Targets {
    $titles = 'proyecto-alfa - TestTarget', 'proyecto-beta - TestTarget', 'demo-api - TestTarget'
    $sizes = '800x600', '900x650', '700x500'
    $list = @()
    for ($i = 0; $i -lt 3; $i++) {
        $p = Start-Process $target -ArgumentList "--title `"$($titles[$i])`" --size $($sizes[$i])" -PassThru
        $list += $p
    }
    Wait-Visible $list
    return $list
}
function Snapshot($targets) { $targets | ForEach-Object { [pscustomobject]@{ H = $_.MainWindowHandle; Bounds = Frame $_.MainWindowHandle } } }
function Visible($targets) { @($targets | Where-Object { [E2E]::IsWindowVisible($_.MainWindowHandle) }).Count }
function Tracked { if (Test-Path $state) { @((Get-Content $state -Raw | ConvertFrom-Json).windows).Count } else { 0 } }
function Start-TabDock { $p = Start-Process $exe -ArgumentList '--merge-exe', 'TabDock.TestTarget.exe' -PassThru; Start-Sleep -Seconds 5; $p.Refresh(); $p }
function Restored-Same($before) { @($before | Where-Object { [E2E]::IsWindowVisible($_.H) -and (Frame $_.H) -eq $_.Bounds }).Count }

. "$PSScriptRoot\e2e-common.ps1"
Assert-SafeToTest "$env:APPDATA\TabDock"
Get-Process TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force
Backup-UserFiles "$env:APPDATA\TabDock"
try {
    Remove-Item $state -ErrorAction SilentlyContinue

    # ---------------------------------------------------------------- A: guardián
    "Escenario A: agrupar y matar TabDock (el guardián restaura)"
    $targets = Start-Targets
    $before = Snapshot $targets
    $main = Start-TabDock
    Check 'una sola ventana visible tras agrupar' ((Visible $targets) -eq 1) "(visibles: $(Visible $targets))"
    Check 'state.json registra 3 ventanas antes de nada' ((Tracked) -eq 3) "(registradas: $(Tracked))"
    $guardians = @(Get-Process TabDock | Where-Object { $_.Id -ne $main.Id })
    Check 'guardián en ejecución' ($guardians.Count -eq 1)

    $active = $targets | Where-Object { [E2E]::IsWindowVisible($_.MainWindowHandle) } | Select-Object -First 1
    if ($active) {
        $f = Rect $main.MainWindowHandle
        $a = Frame $active.MainWindowHandle
        $ab = $a -split ','
        # Derecha y abajo exactos; arriba, dentro del marco: la barra nativa de TestTarget queda bajo la del marco (ADR 0011).
        $inside = ([int]$ab[0] -gt $f.L) -and ([int]$ab[1] -ge $f.T) -and ([int]$ab[1] -lt $f.T + 80) -and ([int]$ab[2] -eq $f.R) -and ([int]$ab[3] -eq $f.B)
        Check 'ventana activa ajustada al hueco (derecha/abajo exactos, su barra bajo la del marco)' $inside "(marco $($f.L),$($f.T),$($f.R),$($f.B) · activa $a)"
    }

    Stop-Process -Id $main.Id -Force
    Start-Sleep -Seconds 3
    Check 'las 3 ventanas visibles en su posición original' ((Restored-Same $before) -eq 3) "(restauradas igual: $(Restored-Same $before))"
    Check 'state.json vacío tras la recuperación' ((Tracked) -eq 0)
    Check 'guardián terminado' (@(Get-Process TabDock -ErrorAction SilentlyContinue).Count -eq 0)
    $targets | Stop-Process -Force

    # ---------------------------------------------------------------- B: cierre normal
    "Escenario B: agrupar y cerrar el marco con su botón Cerrar"
    $targets = Start-Targets
    $before = Snapshot $targets
    $main = Start-TabDock
    $frame = [System.Windows.Automation.AutomationElement]::FromHandle($main.MainWindowHandle)
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Cerrar')
    $close = $frame.FindFirst('Descendants', $cond)
    Check 'botón Cerrar de Windows accesible por UI Automation' ($null -ne $close)
    if ($close) { $close.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
    Start-Sleep -Seconds 3
    Check 'las 3 ventanas restauradas en su posición original' ((Restored-Same $before) -eq 3) "(restauradas igual: $(Restored-Same $before))"
    Check 'state.json limpio' ((Tracked) -eq 0)
    Check 'TabDock y guardián terminados' (@(Get-Process TabDock -ErrorAction SilentlyContinue).Count -eq 0)
    $targets | Stop-Process -Force

    # ---------------------------------------------------------------- C: sin guardián
    "Escenario C: matar TabDock y su guardián (restaura el siguiente arranque)"
    $targets = Start-Targets
    $before = Snapshot $targets
    $main = Start-TabDock
    Get-Process TabDock | Stop-Process -Force
    Start-Sleep -Seconds 1
    Check 'quedan 2 ventanas ocultas sin TabDock' ((Visible $targets) -eq 1) "(visibles: $(Visible $targets))"
    Check 'state.json conserva las 3 ventanas' ((Tracked) -eq 3)
    $next = Start-Process $exe -PassThru
    Start-Sleep -Seconds 5
    Check 'el siguiente arranque restaura las 3 en su posición' ((Restored-Same $before) -eq 3) "(restauradas igual: $(Restored-Same $before))"
    Check 'state.json limpio' ((Tracked) -eq 0)
    Get-Process TabDock -ErrorAction SilentlyContinue | Stop-Process -Force
    $targets | Stop-Process -Force
}
finally {
    # Pase lo que pase, no deja TabDock ni ventanas de prueba abiertas.
    Get-Process TabDock, TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Restore-UserFiles "$env:APPDATA\TabDock"
}

""
if ($script:failures -eq 0) { "RESULTADO: todo correcto" } else { "RESULTADO: $script:failures fallos" }
