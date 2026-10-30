# ADR 0004 — Botones de ventana de Windows en lugar del semáforo de macOS

- **Estado:** Aceptado (2026-10-30, decisión del usuario tras revisar la Fase 2)
- **Ámbito:** `HostFrameWindow`, `Controls/CaptionButtons`

## Contexto

El diseño original pedía el semáforo de macOS (cerrar/minimizar/zoom a la izquierda). Tras verlo implementado, el
usuario decidió mantener el esquema de Windows: minimizar, maximizar/restaurar y cerrar, arriba a la derecha.

## Decisión

- Barra de título propia de **40 px a todo el ancho** con los botones a la derecha: 46 px de ancho, glifos de
  Segoe Fluent Icons (`E921`, `E922`/`E923`, `E8BB`; Segoe MDL2 Assets en Windows 10, mismos códigos) y cerrar en
  `#C42B1C` al pasar el ratón, como en Windows 11.
- **Revisión (misma fecha):** con el hueco debajo de la barra de TabDock aparecían dos juegos de botones (los de
  TabDock y los de la aplicación gestionada). Se adopta el modelo de Groupy: el hueco empieza **arriba del todo** en la
  columna de contenido y **la barra de título de la propia aplicación hace de barra del grupo**. Sus botones actúan
  sobre el grupo: minimizar → minimiza el marco; maximizar o doble clic → alterna maximizar/restaurar el marco;
  arrastrar → mueve el marco; cerrar → cierra esa ventana (esa pestaña). No se modifica la ventana ajena (ADR 0001).
- Los botones propios de TabDock quedan detrás de la ventana activa y solo se ven con el marco vacío.
- El resto del estilo se mantiene (barra lateral, pestañas, Mica, tipografía, movimiento).
- Alt+clic en zoom (pantalla completa) era propio de macOS; la pantalla completa pasa a **F11**.

## Consecuencias

- Sin doble barra: solo se ve la de la aplicación activa. Su botón cerrar cierra la ventana (no el grupo); separar o
  cerrar el grupo se hace desde la bandeja, el atajo o el menú de TabDock (Fase 4).
- Snap Layouts sobre el botón maximizar requiere responder `HTMAXBUTTON` en `WM_NCHITTEST` (Fase 6).
- Se eliminaron los tokens `Traffic.*`; se añadieron `Caption*` en los tres temas.
