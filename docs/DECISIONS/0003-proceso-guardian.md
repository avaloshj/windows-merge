# ADR 0003 — Proceso guardián para la garantía de restauración

- **Estado:** Aceptado (2026-10-30)
- **Fecha:** 2026-10-30

## Contexto

Con `HideMode = hide`, si TabDock termina de forma inesperada (crash, `taskkill`, fallo de .NET), las ventanas
gestionadas quedan **ocultas** hasta que el usuario vuelva a abrir TabDock. El prompt solo exige recuperación
en el siguiente arranque, lo que deja una ventana de tiempo indefinida con VS Code "perdido" (el proceso sigue
vivo, sin botón en la barra de tareas).

## Decisión

Al arrancar, `TabDock.App` lanza **el mismo ejecutable** con `--guardian <pid>` (sin proyecto nuevo):

1. El guardián no tiene UI, abre el proceso padre con `SYNCHRONIZE` y espera con `WaitForSingleObject`.
2. Si el padre sale con código de salida limpio (0) y `state.json` marca `cleanShutdown = true`, el guardián
   termina sin hacer nada.
3. En cualquier otro caso, lee `state.json` y, para cada `WindowSnapshot` cuyo `HWND` siga siendo válido
   (`IsWindow`) **y** pertenezca al mismo `ProcessId` + `ProcessStartTime` (evita reutilización de HWND),
   aplica `ShowWindow(SW_SHOWNA)` + `SetWindowPlacement`.
4. Coste: ~15–20 MB de memoria, 0 % CPU (bloqueado en espera).
5. La recuperación al siguiente arranque se mantiene como segunda red de seguridad.

## Consecuencias

- Cubre crash y `taskkill` normal. **No** cubre `taskkill /F` del árbol completo ni cierre del guardián; para eso
  queda la recuperación en el próximo arranque.
- El guardián debe compartir la lógica de restauración (`SnapshotRestorer`) con la app: vive en
  `TabDock.Win32` y se prueba una sola vez.
