# ADR 0005 — Reglas, Merge All y agrupado automático

- **Estado:** Aceptado (2026-10-30)
- **Ámbito:** `RuleMatcher`, `GroupingService`, `AutoMergeDebouncer`, `FrameCoordinator`, `settings.json`

## Decisión

1. **Un grupo por regla.** "Merge All Windows" de una regla crea su marco o, si ya existe, le añade las ventanas que
   falten (idempotente). Las ventanas se asocian por **ruta del ejecutable** (y clase opcional), no por PID.
2. **`Ctrl+Alt+M`** agrupa el programa de la ventana en primer plano; si no tiene regla, la primera regla activa.
3. **El agrupado automático solo añade a un grupo existente.** Abrir una ventana de VS Code no crea un marco por sí
   solo: el usuario lo crea con `Ctrl+Alt+M`, la bandeja o "agrupar al iniciar" (`MergeOnStartup`). Así TabDock nunca
   captura ventanas sin una acción previa del usuario.
4. Las ventanas nuevas esperan **150 ms** sin eventos antes de evaluarse (llegan antes de tener título y tamaño) y entran
   **como pestaña activa** sin ocultarse y volver a mostrarse.
5. **Atajos por defecto:** `Ctrl+Alt+M`, `Ctrl+Alt+U`, `Ctrl+Alt+1…9`, `Ctrl+Alt+AvPág/RePág` (siguiente/anterior en
   orden visual) y `Ctrl+Alt+B`. `Ctrl+Alt+Tab` lo reserva Windows (`RegisterHotKey` falla con 1409), por eso no se usa.
   Los atajos en conflicto se notifican desde la bandeja; cambiarlos llega con la ventana de ajustes (Fase 7).
6. **TabDock es residente:** vive en la bandeja (`ShutdownMode.OnExplicitShutdown`); "Salir" restaura todas las
   ventanas. La opción de prueba `--merge-exe` conserva el cierre al quedarse sin marcos.
7. **Nombres de pestaña:** `VsCodeTitleParser` (proyecto, archivo activo, `●`, SSH/WSL/Container, perfil) o
   `GenericTitleParser`; las ventanas sin carpeta se numeran "Ventana vacía N" dentro del grupo; el color de la pestaña
   es determinista por nombre de proyecto.

## Consecuencias

- Con la plantilla de título por defecto de VS Code hay casos ambiguos (perfil sin archivo abierto). La plantilla
  recomendada `${rootName}${separator}${activeEditorShort}${separator}${appName}` los evita; el asistente de la Fase 7
  la ofrecerá (opcional) y elegirá el parser correspondiente.
- Los cambios en `settings.json` se aplican al reiniciar TabDock hasta que exista la ventana de ajustes (Fase 7).
