# ADR 0002 — Modo de ocultación de pestañas inactivas

- **Estado:** Aceptado (2026-10-30)
- **Fecha:** 2026-10-30

## Contexto

Las ventanas inactivas de un grupo no deben verse ni estorbar. Opciones evaluadas:

| | `hide` (`ShowWindow(SW_HIDE)`) | `taskbarOnly` (`ITaskbarList::DeleteTab`) |
|---|---|---|
| Visible en pantalla | No | Sí, apilada detrás de la activa en el slot |
| Barra de tareas | Desaparece | Desaparece (botón eliminado) |
| Alt+Tab / Win+Tab | No aparece | **Sí aparece** (al elegirla, TabDock debe activar su pestaña) |
| Renderizado de Electron | Se pausa/limita (ventana oculta) → menos CPU/GPU | Sigue renderizando |
| Riesgo si TabDock muere | Ventana invisible hasta restaurar | Ventana visible pero sin botón en la barra |
| Estabilidad | Alta | Explorer puede volver a añadir el botón (p. ej. tras reiniciar Explorer o al cambiar el título); requiere re-aplicar en `TaskbarCreated` |

## Decisión

- **Por defecto `hide`.** Es el comportamiento más parecido a macOS (una sola ventana visible por grupo) y el
  más barato en recursos.
- `taskbarOnly` disponible en *Avanzado* para quien quiera conservar las ventanas en Alt+Tab.
- El marco anfitrión tiene **un único botón en la barra de tareas** por grupo; su miniatura muestra la ventana
  activa. Se evaluará en Fase 6 si `ITaskbarList3::SetThumbnailClip` aporta.
- Si una ventana oculta se vuelve a mostrar sola (p. ej. `code archivo.txt` desde la terminal enfoca una
  ventana existente), TabDock lo interpreta como **petición de activación** de esa pestaña.

## Consecuencias

- `hide` hace crítica la restauración: ver ADR 0003 (proceso guardián) y la recuperación desde `state.json`.
- `taskbarOnly` requiere escuchar el mensaje registrado `TaskbarCreated` para re-aplicar `DeleteTab`.
