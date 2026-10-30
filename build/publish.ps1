<#
.SYNOPSIS
    Publica TabDock para distribución: autocontenido, ReadyToRun, x64 y/o arm64; opcionalmente firma y empaqueta.
.DESCRIPTION
    - dotnet publish -c Release -r <rid> --self-contained, PublishReadyToRun=true. PublishTrimmed=false: WPF no
      admite recorte (docs/DECISIONS/0010).
    - -Sign: firma los ejecutables con signtool (Authenticode, SHA-256 y sello de tiempo). El certificado NO está en el
      repositorio: se toma de variables de entorno (definidas por quien firma, nunca en el código):
        TABDOCK_SIGN_CERT_PATH       ruta del .pfx
        TABDOCK_SIGN_CERT_PASSWORD   contraseña del .pfx
        TABDOCK_SIGN_TIMESTAMP_URL   servidor de sellado de tiempo (RFC 3161) de la autoridad del certificado
    - -Pack: crea el instalador y los paquetes de Velopack con la herramienta vpk, si está instalada (misma versión que
      la biblioteca: dotnet tool install -g vpk --version 1.2.0).
    Salida en artifacts\publish\<rid> y artifacts\releases\<rid>.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\publish.ps1 -Runtime win-x64
#>
param(
    [ValidateSet('win-x64', 'win-arm64')] [string[]] $Runtime = @('win-x64', 'win-arm64'),
    [string] $Version = '0.1.0',
    [switch] $Sign,
    [switch] $Pack,
    [string] $Root = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'

function Find-SignTool {
    $kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $tool = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like '*\x64\*' } |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $tool) { throw 'No se encontró signtool.exe (Windows SDK).' }
    $tool.FullName
}

function Invoke-Sign([string[]] $Files) {
    foreach ($name in 'TABDOCK_SIGN_CERT_PATH', 'TABDOCK_SIGN_CERT_PASSWORD', 'TABDOCK_SIGN_TIMESTAMP_URL') {
        if (-not (Get-Item "env:$name" -ErrorAction SilentlyContinue)) { throw "Falta la variable de entorno $name para firmar." }
    }
    $signtool = Find-SignTool
    & $signtool sign /fd SHA256 /td SHA256 /tr $env:TABDOCK_SIGN_TIMESTAMP_URL /f $env:TABDOCK_SIGN_CERT_PATH /p $env:TABDOCK_SIGN_CERT_PASSWORD $Files
    if ($LASTEXITCODE -ne 0) { throw "signtool terminó con código $LASTEXITCODE." }
}

foreach ($rid in $Runtime) {
    $out = Join-Path $Root "artifacts\publish\$rid"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }

    "== Publicando $rid ($Version)"
    dotnet publish (Join-Path $Root 'src\TabDock.App\TabDock.App.csproj') -c Release -r $rid --self-contained true `
        -p:PublishReadyToRun=true -p:PublishTrimmed=false -p:Version=$Version -o $out
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish ($rid) terminó con código $LASTEXITCODE." }

    $size = [math]::Round(((Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 1)
    "   $out ($size MB)"

    if ($Sign) {
        "== Firmando $rid"
        Invoke-Sign @((Join-Path $out 'TabDock.exe'), (Join-Path $out 'TabDock.dll'))
    }

    if ($Pack) {
        if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
            "   vpk no está instalado: se omite el paquete de Velopack (dotnet tool install -g vpk --version 1.2.0)."
            continue
        }

        "== Empaquetando $rid con Velopack"
        $releases = Join-Path $Root "artifacts\releases\$rid"
        $packArgs = @('pack', '--packId', 'TabDock', '--packVersion', $Version, '--packDir', $out, '--mainExe', 'TabDock.exe',
            '--packTitle', 'TabDock', '--runtime', $rid, '--outputDir', $releases)
        if ($Sign) {
            $packArgs += @('--signParams', "/fd SHA256 /td SHA256 /tr $env:TABDOCK_SIGN_TIMESTAMP_URL /f `"$env:TABDOCK_SIGN_CERT_PATH`" /p $env:TABDOCK_SIGN_CERT_PASSWORD")
        }

        vpk @packArgs
        if ($LASTEXITCODE -ne 0) { throw "vpk pack ($rid) terminó con código $LASTEXITCODE." }
        "   $releases"
    }
}
