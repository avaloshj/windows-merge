<#
.SYNOPSIS
    Prueba de extremo a extremo de la Fase 7: introducción, migración de settings.json, ventana de ajustes, inicio con
    Windows y exportación del diagnóstico.
.DESCRIPTION
    Hace copia de settings.json, preferences.json y del valor "TabDock" de la clave Run del usuario, y los restaura al
    terminar. La interfaz se maneja con UI Automation (sin simular teclas ni ratón). Los datos son ficticios.
    La captura de un atajo nuevo necesita teclado real: se comprueba a mano (docs/TEST-PLAN.md).
#>
param([string] $Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.IO.Compression.FileSystem

$exe = "$Root\src\TabDock.App\bin\Debug\net10.0-windows\TabDock.exe"
$data = "$env:APPDATA\TabDock"
$files = 'settings.json', 'preferences.json'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$work = Join-Path ([IO.Path]::GetTempPath()) ("tabdock-e2e-" + [guid]::NewGuid().ToString('N'))
$script:failures = 0
$UIA = [System.Windows.Automation.AutomationElement]

function Check([string] $name, [bool] $ok, [string] $detail = '') { if ($ok) { "  OK    $name $detail" } else { "  FALLO $name $detail"; $script:failures++ } }
function Settings-Json { Get-Content "$data\settings.json" -Raw | ConvertFrom-Json }
function Wait-Window([int] $processId, [string] $name, [int] $seconds = 15) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::ProcessIdProperty, $processId)
    do {
        $found = @($UIA::RootElement.FindAll('Children', $cond)) | ? { $_.Current.Name -eq $name } | Select-Object -First 1
        if ($found) { return $found }
        Start-Sleep -Milliseconds 300
    } while ([DateTime]::UtcNow -lt $deadline)
    return $null
}
function Find($parent, $controlType, [string] $name) {
    $type = New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty, $controlType)
    $named = New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty, $name)
    $parent.FindFirst('Descendants', (New-Object System.Windows.Automation.AndCondition($type, $named)))
}
function Invoke($element) { $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 600 }
function Select-Tab($window, [string] $name) {
    $tab = Find $window ([System.Windows.Automation.ControlType]::TabItem) $name
    $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 500
}
function Choose($window, [string] $comboName, [string] $optionName) {
    $combo = Find $window ([System.Windows.Automation.ControlType]::ComboBox) $comboName
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    # La lista tarda un momento variable en abrirse: se espera al elemento (máx. 3 s) en vez de una pausa fija.
    $item = $null
    for ($i = 0; $i -lt 15 -and $null -eq $item; $i++) { Start-Sleep -Milliseconds 200; $item = Find $combo ([System.Windows.Automation.ControlType]::ListItem) $optionName }
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
    Start-Sleep -Milliseconds 600
}
function Toggle($window, [string] $name) {
    (Find $window ([System.Windows.Automation.ControlType]::CheckBox) $name).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 600
}
function Stop-TabDock { Get-Process TabDock -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 700 }

. "$PSScriptRoot\e2e-common.ps1"
Assert-SafeToTest "$env:APPDATA\TabDock"
Get-Process TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force
Backup-UserFiles "$env:APPDATA\TabDock"
New-Item -ItemType Directory -Force $data, $work | Out-Null
$previousRun = (Get-ItemProperty $runKey -Name TabDock -ErrorAction SilentlyContinue).TabDock
try {
    "1. Primer arranque: introducción"
    Remove-Item "$data\settings.json", "$data\preferences.json" -ErrorAction SilentlyContinue
    $main = Start-Process $exe -PassThru
    $welcome = Wait-Window $main.Id 'Bienvenido a TabDock'
    Check 'se muestra la introducción' ($null -ne $welcome)
    Check 'settings.json nuevo con la introducción pendiente' ((Settings-Json).schemaVersion -eq 2 -and -not (Settings-Json).user.onboardingCompleted)
    if ($welcome) {
        Check 'paso 1 de 3' ($null -ne (Find $welcome ([System.Windows.Automation.ControlType]::Text) 'Paso 1 de 3'))
        Invoke (Find $welcome ([System.Windows.Automation.ControlType]::Button) 'Siguiente')
        Check 'avanza al paso 2' ($null -ne (Find $welcome ([System.Windows.Automation.ControlType]::Text) 'Paso 2 de 3'))
        Invoke (Find $welcome ([System.Windows.Automation.ControlType]::Button) 'Saltar')
        Check 'saltar cierra la ventana' ($null -eq (Wait-Window $main.Id 'Bienvenido a TabDock' 2))
        Check 'y la da por vista' ((Settings-Json).user.onboardingCompleted -eq $true)
    }
    Stop-TabDock

    "2. Migración de un settings.json de la Fase 4 (esquema 1)"
    $now = [DateTime]::UtcNow.ToString('o')
    $v1 = @{ schemaVersion = 1; hotkeys = @()
             rules = @(@{ id = [guid]::NewGuid().ToString(); displayName = 'Programa de prueba'; executableName = 'TabDock.TestTarget.exe'; executablePathPattern = $null
                          windowClassName = $null; titleParser = 'Generic'; enabled = $true; autoMerge = $false; mergeOnStartup = $false; sidebarPosition = 'Left'
                          createdAt = $now; updatedAt = $now }) }
    [IO.File]::WriteAllText("$data\settings.json", ($v1 | ConvertTo-Json -Depth 6))
    $main = Start-Process $exe -ArgumentList '--settings' -PassThru
    $settingsWindow = Wait-Window $main.Id 'Configuración de TabDock'
    $migrated = Settings-Json
    Check 'migrado al esquema 2' ($migrated.schemaVersion -eq 2)
    Check 'la regla se conserva' (@($migrated.rules).Count -eq 1 -and $migrated.rules[0].displayName -eq 'Programa de prueba')
    Check 'un usuario existente no ve la introducción' ($migrated.user.onboardingCompleted -and $null -eq (Wait-Window $main.Id 'Bienvenido a TabDock' 2))

    "3. Ventana de ajustes (--settings)"
    Check 'se abre' ($null -ne $settingsWindow)
    if ($settingsWindow) {
        Select-Tab $settingsWindow 'Apariencia'
        Choose $settingsWindow 'Tema' 'Oscuro'
        Choose $settingsWindow 'Densidad de la barra lateral' 'Compacta'
        Toggle $settingsWindow 'Fondo Mica'
        $user = (Settings-Json).user
        Check 'tema, densidad y Mica se guardan al momento' ($user.theme -eq 'Dark' -and $user.density -eq 'Compact' -and $user.useMica -eq $false) "($($user.theme), $($user.density), mica=$($user.useMica))"

        Select-Tab $settingsWindow 'General'
        Toggle $settingsWindow 'Iniciar TabDock con Windows'
        $value = (Get-ItemProperty $runKey -Name TabDock -ErrorAction SilentlyContinue).TabDock
        Check 'iniciar con Windows escribe la clave Run con la ruta entre comillas' ($value -eq "`"$exe`"")
        Toggle $settingsWindow 'Iniciar TabDock con Windows'
        Check 'y desactivarlo la borra' ($null -eq (Get-ItemProperty $runKey -Name TabDock -ErrorAction SilentlyContinue))

        Select-Tab $settingsWindow 'Programas'
        Toggle $settingsWindow 'Programa de prueba'
        Check 'desactivar un programa se guarda' ((Settings-Json).rules[0].enabled -eq $false)

        Select-Tab $settingsWindow 'Atajos'
        Check 'lista los atajos' ($null -ne (Find $settingsWindow ([System.Windows.Automation.ControlType]::Edit) 'Agrupar ventanas (Merge All)'))
        Select-Tab $settingsWindow 'General'
        Choose $settingsWindow 'Idioma' 'English'
        Check 'cambiar el idioma avisa del reinicio' ($null -ne (Find $settingsWindow ([System.Windows.Automation.ControlType]::Text) 'El idioma cambiará la próxima vez que inicies TabDock.'))
        Check 'y se guarda' ((Settings-Json).user.language -eq 'en')
    }

    "4. Exportar diagnóstico (--export-diagnostics, con TabDock en marcha)"
    $prefs = @{ schemaVersion = 1; tabs = @(@{ ruleId = [guid]::NewGuid().ToString(); projectKey = 'cliente-ficticio'; alias = 'Alias ficticio'; colorHex = $null; pinned = $false; order = $null; lastSeenAt = $now; deletedAt = $null }) }
    [IO.File]::WriteAllText("$data\preferences.json", ($prefs | ConvertTo-Json -Depth 6))
    $zip = Join-Path $work 'diagnostico.zip'
    $export = Start-Process $exe -ArgumentList '--export-diagnostics', "`"$zip`"" -PassThru -Wait
    Check 'termina con código 0' ($export.ExitCode -eq 0)
    Check 'crea el ZIP' (Test-Path $zip)
    if (Test-Path $zip) {
        $archive = [IO.Compression.ZipFile]::OpenRead($zip)
        try {
            $names = @($archive.Entries | % { $_.FullName })
            Check 'contiene LEEME, system, settings y logs' ($names -contains 'LEEME.txt' -and $names -contains 'system.json' -and $names -contains 'settings.json' -and @($names | ? { $_ -like 'logs/*' }).Count -ge 1)
            Check 'no contiene preferencias ni estado' (@($names | ? { $_ -match 'preferences|state' }).Count -eq 0)
            $text = ($archive.Entries | % { $r = New-Object IO.StreamReader($_.Open()); try { $r.ReadToEnd() } finally { $r.Dispose() } }) -join "`n"
            $pattern = '(?<![\p{L}\p{N}_])(' + [regex]::Escape($env:USERNAME) + '|' + [regex]::Escape($env:COMPUTERNAME) + ')(?![\p{L}\p{N}_])'
            Check 'sin nombre de usuario ni de equipo' (-not [regex]::IsMatch($text, $pattern, 'IgnoreCase'))
            Check 'sin alias ni nombres de proyecto' (-not ($text -match 'cliente-ficticio|Alias ficticio|Programa de prueba'))
        } finally { $archive.Dispose() }
    }
    $main.Refresh()
    Check 'TabDock sigue en marcha' (-not $main.HasExited)
}
finally {
    Stop-TabDock
    Restore-UserFiles "$env:APPDATA\TabDock"
    if ($null -ne $previousRun) { Set-ItemProperty $runKey -Name TabDock -Value $previousRun } else { Remove-ItemProperty $runKey -Name TabDock -ErrorAction SilentlyContinue }
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

""
if ($script:failures -eq 0) { "RESULTADO: todo correcto" } else { "RESULTADO: $script:failures fallos" }
