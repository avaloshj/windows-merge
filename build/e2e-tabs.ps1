<#
.SYNOPSIS
    Prueba de extremo a extremo de la Fase 5: preferencias guardadas (alias, fijada, orden) y búsqueda rápida.
.DESCRIPTION
    Usa ventanas TabDock.TestTarget con títulos ficticios, una regla temporal solo para TestTarget y un
    preferences.json temporal; hace copia de settings.json y preferences.json y los restaura al terminar.
    Los atajos se prueban publicando WM_HOTKEY en la ventana de mensajes de TabDock (sin simular teclas) y el texto de
    la búsqueda se escribe con UI Automation (ValuePattern), sin teclado.
    Arrastrar, menú contextual y vista previa necesitan ratón: se verifican a mano (docs/TEST-PLAN.md).
#>
param([string] $Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Tabs5 {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
}
"@

$exe = "$Root\src\TabDock.App\bin\Debug\net10.0-windows\TabDock.exe"
$target = "$Root\src\TabDock.TestTarget\bin\Debug\net10.0-windows\TabDock.TestTarget.exe"
$data = "$env:APPDATA\TabDock"
$files = 'settings.json', 'preferences.json'
$script:failures = 0
function Check([string] $name, [bool] $ok, [string] $detail = '') { if ($ok) { "  OK    $name $detail" } else { "  FALLO $name $detail"; $script:failures++ } }
function Wait-Visible($processes) {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do { Start-Sleep -Milliseconds 250; foreach ($p in $processes) { $p.Refresh() } } while (@($processes | ? { $_.MainWindowHandle -eq 0 }).Count -gt 0 -and [DateTime]::UtcNow -lt $deadline)
    Start-Sleep -Milliseconds 500
}
function Hotkey-Window([int] $processId) {
    $h = [IntPtr]::Zero
    while (($h = [Tabs5]::FindWindowEx([IntPtr]-3, $h, [NullString]::Value, 'TabDock.Hotkeys')) -ne [IntPtr]::Zero) {
        $owner = 0; [Tabs5]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
        if ($owner -eq $processId) { return $h }
    }
    return [IntPtr]::Zero
}
# MergeAll=1 … QuickSearch=6 (orden de los atajos por defecto).
function Press([IntPtr] $hotkeyWindow, [int] $id) { [Tabs5]::PostMessage($hotkeyWindow, 0x0312, [IntPtr]$id, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 1200 }
function Process-Windows([int] $processId) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
    @([System.Windows.Automation.AutomationElement]::RootElement.FindAll('Children', $cond))
}
function List-Items($element) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    @($element.FindAll('Descendants', $cond) | % { $_.Current.Name })
}

. "$PSScriptRoot\e2e-common.ps1"
Assert-SafeToTest "$env:APPDATA\TabDock"
Get-Process TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force
Backup-UserFiles "$env:APPDATA\TabDock"
try {
    New-Item -ItemType Directory -Force $data | Out-Null
    $ruleId = [guid]::NewGuid().ToString()
    $now = [DateTime]::UtcNow.ToString('o')
    $hotkeys = @(
        @{ command = 'MergeAll'; modifiers = 'Alt, Control'; virtualKey = 77; enabled = $true },
        @{ command = 'UnmergeAll'; modifiers = 'Alt, Control'; virtualKey = 85; enabled = $true },
        @{ command = 'NextTab'; modifiers = 'Alt, Control'; virtualKey = 34; enabled = $true },
        @{ command = 'PreviousTab'; modifiers = 'Alt, Control'; virtualKey = 33; enabled = $true },
        @{ command = 'ToggleSidebar'; modifiers = 'Alt, Control'; virtualKey = 66; enabled = $true },
        @{ command = 'QuickSearch'; modifiers = 'Alt, Control'; virtualKey = 80; enabled = $true }
    )
    $settings = @{
        schemaVersion = 1
        rules = @(@{ id = $ruleId; displayName = 'TestTarget'; executableName = 'TabDock.TestTarget.exe'; executablePathPattern = $null; windowClassName = $null
                     titleParser = 'Generic'; enabled = $true; autoMerge = $true; mergeOnStartup = $false; sidebarPosition = 'Left'; createdAt = $now; updatedAt = $now })
        hotkeys = $hotkeys
    }
    $preferences = @{
        schemaVersion = 1
        tabs = @(
            @{ ruleId = $ruleId; projectKey = 'proyecto-beta'; alias = 'Beta renombrada'; colorHex = '#EC4899'; pinned = $true; order = $null; lastSeenAt = $now; deletedAt = $null },
            @{ ruleId = $ruleId; projectKey = 'demo-api'; alias = $null; colorHex = $null; pinned = $false; order = 0; lastSeenAt = $now; deletedAt = $null },
            @{ ruleId = $ruleId; projectKey = 'proyecto-alfa'; alias = $null; colorHex = $null; pinned = $false; order = 1; lastSeenAt = $now; deletedAt = $null }
        )
    }
    [IO.File]::WriteAllText("$data\settings.json", ($settings | ConvertTo-Json -Depth 6))
    [IO.File]::WriteAllText("$data\preferences.json", ($preferences | ConvertTo-Json -Depth 6))

    $targets = 'proyecto-alfa', 'proyecto-beta', 'demo-api' | % { Start-Process $target -ArgumentList "--title `"$_ - TestTarget`"" -PassThru }
    Wait-Visible $targets
    $main = Start-Process $exe -PassThru
    Start-Sleep -Seconds 5
    $hk = Hotkey-Window $main.Id
    Check 'TabDock residente con atajos' ($hk -ne [IntPtr]::Zero)

    "1. Preferencias de la sesión anterior"
    Press $hk 1
    $main.Refresh()
    $frame = [System.Windows.Automation.AutomationElement]::FromHandle($main.MainWindowHandle)
    $tabs = @(List-Items $frame)
    Check 'tres pestañas' ($tabs.Count -eq 3) "($($tabs -join ' | '))"
    Check 'la fijada va primero y muestra su alias' ($tabs.Count -gt 0 -and $tabs[0] -match '^Beta renombrada' -and $tabs[0] -match 'fijada')
    Check 'el resto respeta el orden guardado' ($tabs.Count -eq 3 -and $tabs[1] -match '^demo-api' -and $tabs[2] -match '^proyecto-alfa')

    "2. Búsqueda rápida (Ctrl+Alt+P)"
    Press $hk 6
    $palette = Process-Windows $main.Id | ? { $_.Current.Name -eq 'Búsqueda rápida' } | Select-Object -First 1
    Check 'la paleta se abre' ($null -ne $palette)
    if ($palette) {
        $all = @(List-Items $palette)
        Check 'lista todas las pestañas al abrir' ($all.Count -eq 3) "($($all -join ' | '))"
        $editCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
        $edit = $palette.FindFirst('Descendants', $editCond)
        $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('api')
        Start-Sleep -Milliseconds 400
        $filtered = @(List-Items $palette)   # @(): una lista de un elemento no debe desenvolverse en cadena
        Check 'filtra de forma difusa al escribir' ($filtered.Count -eq 1 -and $filtered[0] -match '^demo-api') "($($filtered -join ' | '))"
        $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('xyz-nada')
        Start-Sleep -Milliseconds 400
        Check 'sin coincidencias, lista vacía' ((List-Items $palette).Count -eq 0)
        # Cerrar desde fuera (como Alt+F4) no debe tumbar TabDock.
        try { $palette.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() } catch { }
        Start-Sleep -Milliseconds 800
        $main.Refresh()
        Check 'cerrar la paleta desde fuera no cierra TabDock' (-not $main.HasExited)
    }

    "3. Las preferencias siguen en disco"
    $saved = Get-Content "$data\preferences.json" -Raw | ConvertFrom-Json
    Check 'alias y fijada conservados' (@($saved.tabs | ? { $_.projectKey -eq 'proyecto-beta' -and $_.alias -eq 'Beta renombrada' -and $_.pinned }).Count -eq 1)
}
finally {
    Get-Process TabDock, TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Restore-UserFiles "$env:APPDATA\TabDock"
}

""
if ($script:failures -eq 0) { "RESULTADO: todo correcto" } else { "RESULTADO: $script:failures fallos" }
