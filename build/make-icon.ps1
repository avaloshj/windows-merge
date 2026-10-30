<#
.SYNOPSIS
    Genera src\TabDock.App\Assets\TabDock.ico (todos los tamaños que usa Windows) a partir de un PNG con transparencia.
.DESCRIPTION
    Tamaños: 16, 20, 24, 32, 40, 48, 64, 128 y 256 px (100–200 % de escala en barra de tareas, título, bandeja y
    Explorador). Cada tamaño se guarda como PNG dentro del .ico (formato admitido desde Windows Vista).
    Para la mejor calidad, el PNG de origen debería ser de 256×256: los tamaños mayores que el origen se amplían.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\make-icon.ps1
#>
param(
    [string] $Source = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\TabDock.App\Assets\TabDock.png'),
    [string] $Output = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\TabDock.App\Assets\TabDock.ico')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$original = [System.Drawing.Image]::FromFile($Source)
try {
    if ($original.Width -lt 256) { "Aviso: el origen mide $($original.Width)x$($original.Height); los tamaños mayores se amplían." }

    $frames = foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.DrawImage($original, 0, 0, $size, $size)
        $graphics.Dispose()

        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        [pscustomobject]@{ Size = $size; Data = $stream.ToArray() }
        $stream.Dispose()
    }
}
finally {
    $original.Dispose()
}

# Formato ICO: cabecera (6 bytes) + una entrada de 16 bytes por imagen + los datos de cada imagen.
$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $out
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }   # 0 significa 256
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)                    # sin paleta; reservado
    $writer.Write([uint16]1); $writer.Write([uint16]32)               # planos; bits por píxel
    $writer.Write([uint32]$frame.Data.Length); $writer.Write([uint32]$offset)
    $offset += $frame.Data.Length
}
foreach ($frame in $frames) { $writer.Write($frame.Data) }
$writer.Flush()
[System.IO.File]::WriteAllBytes($Output, $out.ToArray())
$writer.Dispose()
"Generado $Output ($($sizes -join ', ') px, $([math]::Round((Get-Item $Output).Length / 1KB, 1)) KB)"
