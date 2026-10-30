<#
.SYNOPSIS
    Prueba de extremo a extremo de la Fase 6: barra de tareas, maximizar, Snap y ventanas que no responden.
.DESCRIPTION
    Usa ventanas TabDock.TestTarget con títulos ficticios y una regla temporal solo para TestTarget; hace copia de
    settings.json y preferences.json y los restaura al terminar. No toca otras ventanas del usuario: de la barra de
    tareas solo se leen (y muestran) los botones de TestTarget, de TabDock o del grupo de prueba.
    Los atajos se prueban publicando WM_HOTKEY en la ventana de mensajes de TabDock (sin simular teclas).
    Snap Layouts se reproduce con SetWindowPos sobre la ventana activa (mismo efecto que Win+flecha).
#>
param([string] $Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
public static class Desk6 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; public override string ToString() { return Left + "," + Top + "," + Right + "," + Bottom; } }
    [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsHungAppWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);

    // Sube la ventana por encima de las demás sin activarla (HWND_TOP; SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE).
    public static void RaiseWithoutActivating(IntPtr h) { SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0010); }

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);

    // Borde superior del área de cliente en pantalla: por debajo queda la barra de título nativa.
    public static int ClientTop(IntPtr h) { PerMonitor(); var p = new POINT(); ClientToScreen(h, ref p); return p.Y; }

    [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr h, IntPtr rgn);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] static extern bool PtInRegion(IntPtr rgn, int x, int y);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

    // ¿El punto de pantalla (x, y) pertenece a la región de la ventana? Sin región, toda la ventana es suya.
    public static bool InWindowRegion(IntPtr h, int x, int y) {
        PerMonitor(); RECT w; GetWindowRect(h, out w);
        var rgn = CreateRectRgn(0, 0, 0, 0);
        try { return GetWindowRgn(h, rgn) == 0 ? true : PtInRegion(rgn, x - w.Left, y - w.Top); } finally { DeleteObject(rgn); }
    }

    // true si 'upper' está por encima de 'lower' en el orden Z (GW_HWNDNEXT = 2).
    public static bool IsAbove(IntPtr upper, IntPtr lower) { for (var h = GetWindow(upper, 2); h != IntPtr.Zero; h = GetWindow(h, 2)) if (h == lower) return true; return false; }
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr m, ref MONITORINFO mi);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);

    static void PerMonitor() { SetThreadDpiAwarenessContext(new IntPtr(-4)); }   // coordenadas físicas

    public static RECT Bounds(IntPtr h) { PerMonitor(); RECT r; DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))); return r; }   // DWMWA_EXTENDED_FRAME_BOUNDS
    public static RECT WorkArea(IntPtr h) { PerMonitor(); var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) }; GetMonitorInfo(MonitorFromWindow(h, 2), ref mi); return mi.rcWork; }

    // Coloca la ventana para que su borde visible ocupe la zona (como Snap: compensa los bordes invisibles).
    public static void SnapTo(IntPtr h, RECT zone) {
        PerMonitor();
        RECT outer; GetWindowRect(h, out outer); var inner = Bounds(h);
        int l = inner.Left - outer.Left, t = inner.Top - outer.Top, r = outer.Right - inner.Right, b = outer.Bottom - inner.Bottom;
        SetWindowPos(h, IntPtr.Zero, zone.Left - l, zone.Top - t, zone.Right - zone.Left + l + r, zone.Bottom - zone.Top + t + b, 0x0004 | 0x0010 | 0x4000);   // NOZORDER | NOACTIVATE | ASYNCWINDOWPOS
    }

    // Milisegundos que tarda la ventana en atender un mensaje vacío; -1 si no responde en 3 s.
    public static long Responsiveness(IntPtr h) {
        var sw = Stopwatch.StartNew(); IntPtr res;
        var ok = SendMessageTimeout(h, 0, IntPtr.Zero, IntPtr.Zero, 0x0002, 3000, out res) != IntPtr.Zero;   // WM_NULL, SMTO_ABORTIFHUNG
        return ok ? sw.ElapsedMilliseconds : -1;
    }
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
    while (($h = [Desk6]::FindWindowEx([IntPtr]-3, $h, [NullString]::Value, 'TabDock.Hotkeys')) -ne [IntPtr]::Zero) {
        $owner = 0; [Desk6]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
        if ($owner -eq $processId) { return $h }
    }
    return [IntPtr]::Zero
}
# MergeAll=1, UnmergeAll=2, NextTab=3 (orden de los atajos por defecto).
function Press([IntPtr] $hotkeyWindow, [int] $id) { [Desk6]::PostMessage($hotkeyWindow, 0x0312, [IntPtr]$id, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 1200 }
function Active-Target($handles) { @($handles | ? { [Desk6]::IsWindowVisible($_) }) }
function Near($a, $b, [int] $tolerance = 10) {
    [Math]::Abs($a.Left - $b.Left) -le $tolerance -and [Math]::Abs($a.Top - $b.Top) -le $tolerance -and
    [Math]::Abs($a.Right - $b.Right) -le $tolerance -and [Math]::Abs($a.Bottom - $b.Bottom) -le $tolerance
}
# Botones de la barra de tareas relacionados con la prueba (los demás no se leen ni se muestran).
function Taskbar-Buttons {
    $names = @()
    foreach ($cls in 'Shell_TrayWnd', 'Shell_SecondaryTrayWnd') {
        $h = [IntPtr]::Zero
        while (($h = [Desk6]::FindWindowEx([IntPtr]::Zero, $h, $cls, [NullString]::Value)) -ne [IntPtr]::Zero) {
            $bar = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
            $names += @($bar.FindAll('Descendants', $cond) | % { $_.Current.Name } | ? { $_ -match 'TestTarget|TabDock|Grupo de prueba' })
        }
    }
    , $names
}
function Frame-Tabs([IntPtr] $frame) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    @([System.Windows.Automation.AutomationElement]::FromHandle($frame).FindAll('Descendants', $cond) | % { $_.Current.Name })
}
function Own-Buttons($names) { @($names | ? { $_ -match 'TestTarget' -and $_ -notmatch 'Grupo de prueba' }) }
# Con botones combinados el del marco lleva el nombre de la aplicación; sin combinar, el título del grupo.
function Frame-Buttons($names) { @($names | ? { $_ -match 'Grupo de prueba' -or ($_ -match 'TabDock' -and $_ -notmatch 'TestTarget') }) }

. "$PSScriptRoot\e2e-common.ps1"
Assert-SafeToTest "$env:APPDATA\TabDock"
Get-Process TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force
Backup-UserFiles "$env:APPDATA\TabDock"
try {
    New-Item -ItemType Directory -Force $data | Out-Null
    Remove-Item "$data\preferences.json" -ErrorAction SilentlyContinue
    $now = [DateTime]::UtcNow.ToString('o')
    $settings = @{
        schemaVersion = 1
        rules = @(@{ id = [guid]::NewGuid().ToString(); displayName = 'Grupo de prueba'; executableName = 'TabDock.TestTarget.exe'; executablePathPattern = $null; windowClassName = $null
                     titleParser = 'Generic'; enabled = $true; autoMerge = $false; mergeOnStartup = $false; sidebarPosition = 'Left'; createdAt = $now; updatedAt = $now })
        hotkeys = @(
            @{ command = 'MergeAll'; modifiers = 'Alt, Control'; virtualKey = 77; enabled = $true },
            @{ command = 'UnmergeAll'; modifiers = 'Alt, Control'; virtualKey = 85; enabled = $true },
            @{ command = 'NextTab'; modifiers = 'Alt, Control'; virtualKey = 34; enabled = $true }
        )
    }
    [IO.File]::WriteAllText("$data\settings.json", ($settings | ConvertTo-Json -Depth 6))

    # Como VS Code (Chromium): al ocultarse avisan de "destruida" sin destruirse. TabDock no debe soltarlas.
    $normal = @('proyecto-alfa', 'proyecto-beta' | % { Start-Process $target -ArgumentList "--title `"$_ - TestTarget`" --destroy-event-on-hide" -PassThru })
    # Deja de responder 30 s después de mostrarse: para entonces ya está agrupada y oculta.
    $slow = Start-Process $target -ArgumentList '--title "panel-lento - TestTarget" --hang-after 30000' -PassThru
    $all = $normal + $slow
    Wait-Visible $all
    $handles = @($all | % { $_.MainWindowHandle })
    $slowHandle = $slow.MainWindowHandle
    Start-Sleep -Milliseconds 1500
    $before = Taskbar-Buttons
    $main = Start-Process $exe -PassThru
    Start-Sleep -Seconds 5
    $hk = Hotkey-Window $main.Id
    Check 'TabDock residente con atajos' ($hk -ne [IntPtr]::Zero)

    Press $hk 1
    $main.Refresh()
    $frame = $main.MainWindowHandle
    Check 'grupo creado con tres pestañas' ((Frame-Tabs $frame).Count -eq 3)
    if ((Active-Target $handles)[0] -eq $slowHandle) { Press $hk 3 }
    Check 'una sola ventana gestionada visible' ((Active-Target $handles).Count -eq 1)

    "1. Un solo botón en la barra de tareas por grupo"
    Start-Sleep -Milliseconds 1500
    $after = Taskbar-Buttons
    Check 'antes de agrupar, TestTarget tenía botón' ((Own-Buttons $before).Count -gt 0) "($($before -join ' | '))"
    Check 'después, ningún botón propio de las ventanas agrupadas' ((Own-Buttons $after).Count -eq 0) "($($after -join ' | '))"
    Check 'el marco tiene su botón' ((Frame-Buttons $after).Count -ge 1) "($($after -join ' | '))"
    Press $hk 3
    Start-Sleep -Milliseconds 800
    $afterSwitch = Taskbar-Buttons
    Check 'al cambiar de pestaña sigue habiendo uno solo' ((Own-Buttons $afterSwitch).Count -eq 0) "($($afterSwitch -join ' | '))"
    Check 'un aviso de cierre falso al ocultar no saca la ventana del grupo' ((Frame-Tabs $frame).Count -eq 3 -and (Active-Target $handles).Count -eq 1)
    if ((Active-Target $handles)[0] -eq $slowHandle) { Press $hk 3 }
    $active = (Active-Target $handles)[0]

    "2. El marco va encima de la ventana y la deja ver por un hueco (tapa su sombra)"
    $win = [Desk6]::Bounds($active); $fr = [Desk6]::Bounds($frame)
    Check 'el marco está encima de la ventana activa' ([Desk6]::IsAbove($frame, $active))
    Check 'el centro de la ventana cae en el hueco (no es del marco)' (-not [Desk6]::InWindowRegion($frame, [int](($win.Left + $win.Right) / 2), [int](($win.Top + $win.Bottom) / 2)))
    $clientTop = [Desk6]::ClientTop($active)
    Check 'cerca de los bordes del contenido también' (-not [Desk6]::InWindowRegion($frame, $win.Left + 4, $clientTop + 4) -and -not [Desk6]::InWindowRegion($frame, $win.Right - 5, $win.Bottom - 5))
    # TestTarget tiene barra de título nativa: queda bajo la barra del marco (ADR 0011).
    Check 'la barra de título nativa la tapa el marco' ($clientTop -gt $win.Top -and [Desk6]::InWindowRegion($frame, [int](($win.Left + $win.Right) / 2), [int](($win.Top + $clientTop) / 2))) "(barra nativa de $($clientTop - $win.Top) px)"
    # La línea de 1 px del borde de la ventana queda tapada por el marco (unión sin corte con la barra lateral).
    Check 'la línea del borde de la ventana la tapa el marco' ([Desk6]::InWindowRegion($frame, $win.Left, [int](($win.Top + $win.Bottom) / 2)) -and [Desk6]::InWindowRegion($frame, $win.Right - 1, [int](($win.Top + $win.Bottom) / 2)))
    Check 'la barra lateral sigue siendo del marco' ([Desk6]::InWindowRegion($frame, $fr.Left + 20, [int](($fr.Top + $fr.Bottom) / 2)))
    Check 'justo a la izquierda de la ventana (donde caía la sombra) es marco' ([Desk6]::InWindowRegion($frame, $win.Left - 3, [int](($win.Top + $win.Bottom) / 2)))

    "2b. Maximizar desde la barra de la ventana gestionada alterna el marco"
    [Desk6]::ShowWindowAsync($active, 3) | Out-Null   # SW_MAXIMIZE: como su botón maximizar
    Start-Sleep -Milliseconds 1500
    Check 'el marco queda maximizado' ([Desk6]::IsZoomed($frame))
    Check 'la ventana vuelve a su hueco sin maximizar' (-not [Desk6]::IsZoomed($active) -and [Desk6]::IsWindowVisible($active))
    # Visible no basta: el marco maximizado no debe taparla (orden Z).
    Check 'el marco sigue encima de la ventana' ([Desk6]::IsAbove($frame, $active))
    $win = [Desk6]::Bounds($active)
    Check 'y el hueco sigue a la ventana maximizada' (-not [Desk6]::InWindowRegion($frame, [int](($win.Left + $win.Right) / 2), [int](($win.Top + $win.Bottom) / 2)))
    [Desk6]::ShowWindowAsync($active, 3) | Out-Null
    Start-Sleep -Milliseconds 1500
    Check 'segunda vez: el marco se restaura' (-not [Desk6]::IsZoomed($frame))
    Check 'y el marco sigue encima' ([Desk6]::IsAbove($frame, $active))

    "3. Snap de la ventana activa mueve el grupo entero"
    $work = [Desk6]::WorkArea($active)
    $zone = New-Object Desk6+RECT
    $zone.Left = $work.Left; $zone.Top = $work.Top; $zone.Right = $work.Left + [int](($work.Right - $work.Left) / 2); $zone.Bottom = $work.Bottom
    [Desk6]::SnapTo($active, $zone)
    Start-Sleep -Milliseconds 1500
    $frameBounds = [Desk6]::Bounds($frame)
    Check 'el marco ocupa la mitad izquierda' (Near $frameBounds $zone) "(zona $zone, marco $frameBounds)"
    $activeBounds = [Desk6]::Bounds($active)
    Check 'la ventana queda dentro del marco' ($activeBounds.Left -ge $frameBounds.Left - 2 -and $activeBounds.Right -le $frameBounds.Right + 2 -and $activeBounds.Bottom -le $frameBounds.Bottom + 2) "(ventana $activeBounds)"

    "4. Una pestaña que no responde no bloquea TabDock"
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while (-not [Desk6]::IsHungAppWindow($slowHandle) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500 }
    Check 'Windows marca la ventana como colgada' ([Desk6]::IsHungAppWindow($slowHandle))
    Start-Sleep -Seconds 4   # comprobación periódica de salud
    $tabs = Frame-Tabs $frame
    Check 'la pestaña indica "no responde"' (@($tabs | ? { $_ -match '^panel-lento' -and $_ -match 'no responde' }).Count -eq 1) "($($tabs -join ' | '))"
    $worst = 0
    for ($i = 0; $i -lt 3; $i++) {
        Press $hk 3
        $ms = [Desk6]::Responsiveness($frame)
        if ($ms -lt 0) { $worst = 99999 } elseif ($ms -gt $worst) { $worst = $ms }
    }
    Check 'TabDock responde al recorrer todas las pestañas' ($worst -lt 500) "(peor espera: $worst ms)"
    $main.Refresh()
    Check 'TabDock sigue vivo' (-not $main.HasExited)
}
finally {
    Get-Process TabDock, TabDock.TestTarget -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Restore-UserFiles "$env:APPDATA\TabDock"
}

""
if ($script:failures -eq 0) { "RESULTADO: todo correcto" } else { "RESULTADO: $script:failures fallos" }
