<#
.SYNOPSIS
    Prueba de extremo a extremo de la Fase 4: Merge All con regla, agrupado automático, atajos y nombres de pestaña.
.DESCRIPTION
    Usa ventanas TabDock.TestTarget con títulos ficticios y una configuración temporal con una única regla activa
    (TestTarget); la regla de VS Code queda desactivada para no tocar ventanas reales. Hace copia de settings.json y la
    restaura al terminar. Los atajos se prueban publicando WM_HOTKEY en la ventana de mensajes de TabDock (lo mismo que
    hace Windows al pulsarlos), sin simular teclas que llegarían a otras aplicaciones.
#>
param([string] $Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Rules {
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
}
"@

$exe = "$Root\src\TabDock.App\bin\Debug\net10.0-windows\TabDock.exe"
$target = "$Root\src\TabDock.TestTarget\bin\Debug\net10.0-windows\TabDock.TestTarget.exe"
$settingsPath = "$env:APPDATA\TabDock\settings.json"
$statePath = "$env:APPDATA\TabDock\state.json"
$script:failures = 0
function Check([string] $name, [bool] $ok, [string] $detail = '') { if ($ok) { "  OK    $name $detail" } else { "  FALLO $name $detail"; $script:failures++ } }
function Wait-Visible($processes) {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do { Start-Sleep -Milliseconds 250; foreach ($p in $processes) { $p.Refresh() } } while (@($processes | ? { $_.MainWindowHandle -eq 0 }).Count -gt 0 -and [DateTime]::UtcNow -lt $deadline)
    Start-Sleep -Milliseconds 500
}
function Start-Target([string] $title) { $p = Start-Process $target -ArgumentList "--title `"$title`"" -PassThru; Wait-Visible @($p); $p }
function Tracked { if (Test-Path $statePath) { @((Get-Content $statePath -Raw | ConvertFrom-Json).windows).Count } else { 0 } }
function Hotkey-Window([int] $processId) {
    $h = [IntPtr]::Zero
    while (($h = [Rules]::FindWindowEx([IntPtr]-3, $h, [NullString]::Value, 'TabDock.Hotkeys')) -ne [IntPtr]::Zero) {
        $owner = 0; [Rules]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
        if ($owner -eq $processId) { return $h }
    }
    return [IntPtr]::Zero
}
# PowerShell convierte $null en "" para parámetros string de P/Invoke: [NullString]::Value pasa un nulo real.
# Identificadores de atajo: orden de los atajos por defecto (MergeAll=1, UnmergeAll=2, NextTab=3, PreviousTab=4, ToggleSidebar=5, QuickSearch=6, GoToTab1=7…).
function Press([IntPtr] $hotkeyWindow, [int] $id) { [Rules]::PostMessage($hotkeyWindow, 0x0312, [IntPtr]$id, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 900 }
function Frame-Tabs([int] $processId) {
    $p = Get-Process -Id $processId; $p.Refresh()
    if ($p.MainWindowHandle -eq 0) { return @() }
    $frame = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    @($frame.FindAll('Descendants', $cond) | % { $_.Current.Name })
}
function Visible($handles) { @($handles | ? { [Rules]::IsWindowVisible($_) }) }

. "$PSScriptRoot\e2e-common.ps1"
Assert-SafeToTest "$env:APPDATA\TabDock"
Get-Process TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force
Backup-UserFiles "$env:APPDATA\TabDock"

# Configuración temporal: solo la regla de TestTarget activa.
# Si no había settings.json, el que se crea aquí es de la prueba (Restore-UserFiles lo borra al terminar).
if (-not (Test-Path $settingsPath)) {
    $p = Start-Process $exe -ArgumentList '--merge-exe', 'NoExiste.exe' -PassThru; Start-Sleep 4; Get-Process TabDock -ErrorAction SilentlyContinue | Stop-Process -Force
}
try {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    $rule = $settings.rules[0].PSObject.Copy()
    $rule.id = [guid]::NewGuid().ToString()
    $rule.displayName = 'TestTarget'
    $rule.executableName = 'TabDock.TestTarget.exe'
    $rule.windowClassName = $null
    $rule.titleParser = 'Generic'
    $rule.enabled = $true; $rule.autoMerge = $true; $rule.mergeOnStartup = $false
    foreach ($r in $settings.rules) { $r.enabled = $false }
    # La introducción del primer arranque (Fase 7) taparía las ventanas de prueba.
    if ($settings.PSObject.Properties['user']) { $settings.user.onboardingCompleted = $true }
    $settings.rules = @($rule) + @($settings.rules)
    $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8

    $a = Start-Target 'proyecto-alfa - TestTarget'
    $b = Start-Target 'proyecto-beta - TestTarget'
    $main = Start-Process $exe -PassThru
    Start-Sleep -Seconds 5

    "1. Residente en bandeja, sin agrupar al iniciar"
    Check 'TabDock en ejecución sin marco' (-not $main.HasExited -and (Visible @($a.MainWindowHandle, $b.MainWindowHandle)).Count -eq 2)
    $hk = Hotkey-Window $main.Id
    Check 'ventana de atajos encontrada' ($hk -ne [IntPtr]::Zero)

    "2. Ctrl+Alt+M (Merge All)"
    Press $hk 1
    $tabs = Frame-Tabs $main.Id
    Check 'dos pestañas con nombre de proyecto' (($tabs.Count -eq 2) -and ($tabs -join '|') -match 'proyecto-alfa' -and ($tabs -join '|') -match 'proyecto-beta') "($($tabs -join ' | '))"
    Check 'una ventana visible y estado persistido' (((Visible @($a.MainWindowHandle, $b.MainWindowHandle)).Count -eq 1) -and ((Tracked) -eq 2))
    Press $hk 1
    Check 'repetir Ctrl+Alt+M no duplica pestañas' ((Frame-Tabs $main.Id).Count -eq 2)

    "3. Agrupado automático de una ventana nueva"
    $c = Start-Target 'demo-api - TestTarget'
    Start-Sleep -Milliseconds 800
    $tabs = Frame-Tabs $main.Id
    Check 'la ventana nueva entra como pestaña' (($tabs.Count -eq 3) -and ($tabs -join '|') -match 'demo-api') "($($tabs -join ' | '))"
    $visible = Visible @($a.MainWindowHandle, $b.MainWindowHandle, $c.MainWindowHandle)
    Check 'y pasa a ser la activa' (($visible.Count -eq 1) -and ($visible[0] -eq $c.MainWindowHandle))

    "4. Atajos de pestaña"
    Press $hk 7
    $visible = Visible @($a.MainWindowHandle, $b.MainWindowHandle, $c.MainWindowHandle)
    Check 'Ctrl+Alt+1 activa la primera pestaña' (($visible.Count -eq 1) -and ($visible[0] -ne $c.MainWindowHandle))
    $first = $visible[0]
    Press $hk 3
    $visible = Visible @($a.MainWindowHandle, $b.MainWindowHandle, $c.MainWindowHandle)
    Check 'Ctrl+Alt+AvPág pasa a la siguiente' (($visible.Count -eq 1) -and ($visible[0] -ne $first))
    Press $hk 4
    $visible = Visible @($a.MainWindowHandle, $b.MainWindowHandle, $c.MainWindowHandle)
    Check 'Ctrl+Alt+RePág vuelve a la anterior' (($visible.Count -eq 1) -and ($visible[0] -eq $first))

    "5. Ctrl+Alt+U (separar todas)"
    Press $hk 2
    Check 'las tres ventanas visibles' ((Visible @($a.MainWindowHandle, $b.MainWindowHandle, $c.MainWindowHandle)).Count -eq 3)
    Check 'state.json limpio' ((Tracked) -eq 0)
    Check 'TabDock sigue residente' (-not $main.HasExited)
}
finally {
    Get-Process TabDock, TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Restore-UserFiles "$env:APPDATA\TabDock"
}

""
if ($script:failures -eq 0) { "RESULTADO: todo correcto" } else { "RESULTADO: $script:failures fallos" }
