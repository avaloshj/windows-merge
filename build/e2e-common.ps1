# Funciones comunes de los scripts build/e2e-*.ps1: protegen los datos y las ventanas reales del usuario.
# Se cargan con: . "$PSScriptRoot\e2e-common.ps1"
#
# Reglas:
# - Un TabDock que no lanzó la prueba no se cierra: podría tener ventanas reales agrupadas (su guardián caería con él).
# - Una copia .e2e-backup que ya existe es de una prueba interrumpida y contiene el archivo REAL del usuario: se
#   restaura antes de empezar; nunca se sobrescribe.
# - state.json con ventanas que siguen existiendo es información de recuperación: no se borra.
# - No cortar la salida de estos scripts con Select-Object -First: PowerShell los detiene sin ejecutar su limpieza.

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class E2ECommon {
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
}
"@

$script:UserDataFiles = 'settings.json', 'preferences.json'

function Assert-SafeToTest([string] $DataDir) {
    # El guardián de una prueba anterior tarda un instante en terminar tras su TabDock: se le dan unos segundos.
    for ($i = 0; $i -lt 20 -and (Get-Process TabDock -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 250 }
    if (Get-Process TabDock -ErrorAction SilentlyContinue) {
        'TabDock está en marcha: ciérralo (bandeja → Salir) antes de las pruebas.'
        exit 1
    }

    foreach ($f in $script:UserDataFiles) {
        if (Test-Path "$DataDir\$f.e2e-backup") {
            Move-Item "$DataDir\$f.e2e-backup" "$DataDir\$f" -Force
            "Restaurado $f de una prueba anterior interrumpida."
        }
    }

    $statePath = "$DataDir\state.json"
    if (Test-Path $statePath) {
        $state = Get-Content $statePath -Raw | ConvertFrom-Json
        $alive = @($state.windows | Where-Object { [E2ECommon]::IsWindow([IntPtr][long]$_.snapshot.identity.handle) }).Count
        if ($alive -gt 0) {
            "Hay $alive ventana(s) pendientes de restaurar de una sesión anterior: abre TabDock una vez (las restaura al arrancar) y vuelve a lanzar la prueba."
            exit 1
        }

        # Solo referencia ventanas que ya no existen (p. ej. de una prueba anterior): no hay nada que recuperar.
        Remove-Item $statePath -ErrorAction SilentlyContinue
    }
}

function Backup-UserFiles([string] $DataDir) {
    New-Item -ItemType Directory -Force $DataDir | Out-Null
    foreach ($f in $script:UserDataFiles) {
        if (Test-Path "$DataDir\$f") { Copy-Item "$DataDir\$f" "$DataDir\$f.e2e-backup" }
    }
}

function Restore-UserFiles([string] $DataDir) {
    foreach ($f in $script:UserDataFiles) {
        if (Test-Path "$DataDir\$f.e2e-backup") { Move-Item "$DataDir\$f.e2e-backup" "$DataDir\$f" -Force }
        else { Remove-Item "$DataDir\$f" -ErrorAction SilentlyContinue }
    }
}
