# ADR 0011 — Barra de título nativa oculta bajo la barra del marco

- **Estado:** Aceptado (2026-10-30). Completa la ADR 0004 (botones de Windows y "modelo Groupy").
- **Ámbito:** `TabGroupSession` (`TargetBounds`, `UpdateCaptionInset`), `IWindowSystem.GetCaptionInset`,
  `IHostFrame.SetOwnCaptionVisible`

## Contexto

Con el modelo Groupy (ADR 0004), la barra de título de la ventana gestionada hace de barra del grupo. En VS Code con
barra personalizada esa barra es contenido de VS Code (menús), pero en una ventana con barra **nativa** se veía la
barra de Windows del programa encima del contenido, y el usuario pidió quitarla. Quitarla cambiando estilos de una
ventana ajena (`WS_CAPTION`) se descartó en las ADR 0004 y 0007.

## Decisión

1. Se mide la barra nativa de cada ventana al colocarla: distancia del borde visible superior al área de cliente
   (`ClientToScreen` frente a `DWMWA_EXTENDED_FRAME_BOUNDS`). Con barra propia mide 0.
2. **Barra nativa (mide más de 0):**
   - El marco muestra su propia barra: título de la pestaña y botones de Windows.
   - El hueco empieza debajo de esa barra.
   - La ventana se coloca subida lo que mide su barra nativa: el contenido ocupa el hueco y la barra nativa queda bajo
     la barra del marco, que está encima (ADR 0009) y la tapa.
3. **Barra propia (mide 0, p. ej. VS Code con `titleBarStyle: custom`):** sin cambios. La barra de la ventana hace de
   barra del grupo y la del marco queda oculta, para no duplicar botones.
4. El modo cambia con la pestaña activa: al pasar de una ventana nativa a VS Code, el marco oculta su barra y viceversa.
   Cambiar la barra mueve el hueco; la ventana se recoloca enseguida y otra vez cuando el marco termina el layout.

## Consecuencias

- No se modifica ninguna ventana ajena: la barra nativa solo queda tapada.
- Con barra nativa oculta, minimizar, maximizar y cerrar se hacen con la barra del marco: cerrar el marco separa todas
  las ventanas. El doble clic en la barra nativa ya no es posible; sí en la del marco.
- Si la barra nativa fuese más alta que la del marco (40 DIP), su parte superior asomaría por encima del marco. Con las
  escalas habituales no ocurre: 38 px a 125 % frente a 50 px de la barra del marco.
- `e2e-desktop.ps1` comprueba que la barra nativa queda bajo el marco y que el contenido cae en el hueco;
  `e2e-recovery.ps1` y `e2e-sync.ps1` aceptan que la ventana empiece dentro del marco, por debajo de su borde superior.
