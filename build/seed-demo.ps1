<#
.SYNOPSIS
    Abre varias instancias de TabDock.TestTarget con títulos ficticios para probar TabDock sin VS Code.
.EXAMPLE
    ./build/seed-demo.ps1
    ./build/seed-demo.ps1 -IncludeEdgeCases
#>
[CmdletBinding()]
param(
    [switch] $IncludeEdgeCases,
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/TabDock.TestTarget/TabDock.TestTarget.csproj'

dotnet build $project -c $Configuration --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "La compilación de TabDock.TestTarget falló ($LASTEXITCODE)." }

$exe = Join-Path $root "src/TabDock.TestTarget/bin/$Configuration/net10.0-windows/TabDock.TestTarget.exe"

$targets = @(
    @('--title', 'proyecto-alfa - TabDock.TestTarget'),
    @('--title', 'proyecto-beta - TabDock.TestTarget', '--size', '900x650'),
    @('--title', 'demo-api - TabDock.TestTarget', '--size', '1000x700')
)

if ($IncludeEdgeCases) {
    $targets += , @('--title', 'herramienta-ficticia', '--tool-window')
    $targets += , @('--title', 'ventana-pequena', '--size', '250x150')
    $targets += , @('--title', 'con-dialogo - TabDock.TestTarget', '--owner-dialog')
}

foreach ($arguments in $targets) {
    # Windows PowerShell 5.1 no entrecomilla los argumentos con espacios: se hace aquí.
    $line = ($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    Start-Process -FilePath $exe -ArgumentList $line | Out-Null
}

Write-Host "Abiertas $($targets.Count) ventanas de prueba."
