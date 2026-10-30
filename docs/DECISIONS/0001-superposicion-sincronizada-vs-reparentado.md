# ADR 0001 — Superposición sincronizada en lugar de reparentado

- **Estado:** Aceptado (2026-10-30)
- **Fecha:** 2026-10-30
- **Ámbito:** `TabDock.Win32` (`WindowSlotController`), `TabDock.App` (`HostFrameWindow`)

## Contexto

TabDock debe mostrar varias ventanas de primer nivel de otro proceso (VS Code / Electron) como pestañas
dentro de un marco propio. Hay dos familias de soluciones:

1. **Reparentado:** `SetParent(hwndAjeno, hwndSlot)` y quitar `WS_CAPTION`/`WS_THICKFRAME`. La ventana ajena
   se convierte en hija de una ventana de otro proceso.
2. **Superposición sincronizada (modelo Groupy/WindowTabs):** la ventana ajena sigue siendo de primer nivel;
   TabDock la coloca exactamente sobre el hueco del marco y sincroniza posición, tamaño, orden Z y visibilidad.

La documentación de `SetParent` advierte que, al cambiar entre ventana hija y de primer nivel, el llamante
debe actualizar `WS_CHILD`/`WS_POPUP` y que hacerlo entre procesos acopla las colas de entrada de ambos
hilos (`AttachThreadInput` implícito). En Chromium/Electron esto produce, entre otros:

- Pérdida de foco de teclado y fallos de IME (el árbol de ventanas de Chromium asume ser raíz).
- DPI incorrecto: la hija hereda el contexto DPI del padre de otro proceso; Chromium calcula escalas erróneas.
- Menús contextuales, tooltips y popups posicionados en coordenadas de pantalla equivocadas.
- Problemas de composición por GPU (`DirectComposition`) y parpadeos.
- Si TabDock se cuelga, la ventana ajena se cuelga con él (colas de entrada acopladas). Si TabDock muere,
  la ventana hija queda destruida o huérfana.

## Decisión

**Se adopta la superposición sincronizada. Queda prohibido `SetParent` sobre ventanas de otros procesos.**

1. `HostFrameWindow` (WPF, primer nivel, proceso TabDock) dibuja el chrome macOS y la barra lateral. Su área de
   contenido (el *slot*) es un panel **opaco** pintado con `BgWindow` — no transparente — para evitar
   `AllowsTransparency=true` (ventana en capas, rendimiento pobre y sin Mica). La ventana gestionada lo cubre.
2. La ventana activa del grupo se posiciona sobre el slot con `SetWindowPos`/`DeferWindowPos`, compensando
   los bordes invisibles de Windows 10/11 con la diferencia entre `GetWindowRect` y
   `DWMWA_EXTENDED_FRAME_BOUNDS`.
3. Orden Z: tras cada activación se reordena el **marco justo debajo** de la ventana gestionada
   (`SetWindowPos(hwndHost, hwndManaged, …, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)`). No se usan relaciones de
   propietario (`GWLP_HWNDPARENT`) porque obligarían a modificar la ventana ajena.
4. Ventanas inactivas: ocultas según `HideMode` (ver ADR 0002).
5. No se modifican estilos de ventanas ajenas. Existe una opción experimental (desactivada por defecto) para
   quitar `WS_CAPTION`, siempre revertida en la restauración.

## Consecuencias

**Positivas**
- Foco, IME, DPI, GPU y menús de VS Code funcionan exactamente igual que sin TabDock.
- Un cuelgue o cierre de TabDock no afecta a las ventanas gestionadas; restaurar = mostrar y reposicionar.
- No requiere inyección de código ni privilegios.

**Negativas / a mitigar**
- Durante el redimensionado en vivo del marco puede verse el fondo del slot 1–2 frames (Electron repinta
  tarde). Mitigación: `DeferWindowPos` en el mismo ciclo, fondo del slot del color del tema de VS Code.
- Hay que vigilar que otra ventana no se interponga entre marco y ventana activa (eventos de foreground).
- Doble chrome: VS Code en Windows dibuja su propia barra de título y botones. Se documenta en el onboarding;
  la opción experimental lo resuelve parcialmente.
- Maximizar/minimizar desde la ventana gestionada debe interceptarse vía eventos y redirigirse al marco.

## Alternativas descartadas

| Alternativa | Motivo |
|---|---|
| `SetParent` + quitar estilos | Problemas de foco/IME/DPI/GPU descritos arriba; acopla la estabilidad de VS Code a TabDock. |
| Thumbnails DWM como contenido (`DwmRegisterThumbnail`) y reenviar entrada | Los thumbnails no son interactivos; reenviar entrada sintética es frágil e intrusivo. Solo se usan para vistas previas. |
| Hooks globales con DLL inyectada (`SetWindowsHookEx` global) | Prohibido por la política de seguridad del proyecto. |
| Cloaking DWM de ventanas ajenas (`DWMWA_CLOAK`) | `DwmSetWindowAttribute(DWMWA_CLOAK)` solo funciona sobre ventanas del propio proceso. |
