# ADR 0009 — Marco por encima de la ventana activa, con un hueco

- **Estado:** Aceptado (2026-10-30). Revisa el orden Z de la ADR 0001 (la ventana gestionada sigue sin reparentarse).
- **Ámbito:** `TabGroupSession`, `IHostFrame.SetSlotHole`, `SlotHole`, `WindowSystem.PlaceAbove`, `HostFrameWindow`

## Contexto

Hasta ahora el marco iba justo **debajo** de la ventana activa. Windows dibuja la sombra de toda ventana de nivel
superior, y la de VS Code caía sobre la barra lateral. No se puede quitar desde fuera sin efectos peores: desactivar el
dibujo de DWM (`DWMWA_NCRENDERING_POLICY`) hace que Windows pinte un borde clásico en los bordes invisibles de VS Code,
y una región sobre la ventana ajena no desactiva la sombra (WIN32-NOTES, Fase 7).

## Decisión

1. El marco va justo **encima** de la ventana activa (`PlaceAbove`: se coloca tras la ventana que la precede; si esa es
   "siempre visible" o no hay ninguna, `HWND_TOP`, para no heredar el "siempre visible").
2. La ventana del marco tiene una región (`SetWindowRgn` sobre la ventana **propia**) con un hueco del tamaño exacto del
   área visible de la ventana activa. Por el hueco se ve la ventana y los clics le llegan a ella; su sombra queda bajo
   las partes opacas del marco.
3. Regla del grupo: el marco siempre por encima de la ventana activa. Se reaplica al mostrar una pestaña, cuando la
   ventana pasa a primer plano (clic a través del hueco), al activarse el marco, tras maximizar y en cada cambio de orden
   Z del marco.
4. El hueco se recalcula al mover, redimensionar, maximizar, cambiar de DPI o de ancho de barra lateral, y tras cambiar
   `WindowChrome` (que borra la región). Minimizado o sin ventana visible, el hueco se cierra.
5. `WindowChrome` siempre con vidrio (`GlassFrameThickness` de al menos 1 px): sin vidrio, `WindowChrome` fija su propia
   región en cada movimiento y borraría el hueco.
6. Con el hueco abierto, el marco usa fondo sólido: Mica sobre una ventana con región no se ha podido verificar (exige
   capturar la pantalla). Sin ventanas (marco vacío, `--demo-frame`), Mica sigue disponible.

7. **Unión sin línea entre barra lateral y ventana:** el hueco es 1 px más pequeño por lado (2 px por encima del 100 %
   de escala) y el marco tapa la línea que Windows 11 y VS Code dibujan en el borde de la ventana. La franja tapada usa
   el color de la barra lateral. El separador de la barra lateral no muestra línea en reposo (sí con ratón o foco, y
   siempre en alto contraste, mediante `Brush.SplitterLine`).

## Consecuencias

- No se modifica ninguna ventana ajena para quitar la sombra. La supresión con `DWMWA_NCRENDERING_POLICY` se mantiene
  solo para ventanas sin ningún marco nativo, donde es segura.
- Al hacer clic en VS Code, Windows lo sube por encima del marco un instante, hasta que el marco vuelve encima: puede
  verse la sombra durante un parpadeo.
- Una ventana con región pierde la sombra propia de DWM y las esquinas redondeadas: el marco se ve plano y con
  esquinas rectas mientras tiene ventanas. Recuperar Mica y las esquinas queda para la Fase 8.
- `e2e-desktop.ps1` comprueba el orden Z y la región (`PtInRegion`): el centro y los bordes de la ventana caen en el
  hueco, y la barra lateral, la franja donde caía la sombra y la línea del borde de la ventana son del marco.
- Se tapan 1–2 px del borde del contenido de la ventana.
