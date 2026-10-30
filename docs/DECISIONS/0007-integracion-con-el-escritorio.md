# ADR 0007 — Integración con el escritorio de Windows (robustez)

- **Estado:** Aceptado (2026-10-30)
- **Ámbito:** `TabGroupSession`, `WindowSystem.Shell`, `VirtualDesktops`, `HostFrameWindow`, `FrameCoordinator`

## Contexto

Con el modelo de superposición (ADR 0001) cada ventana del grupo sigue siendo una ventana de nivel superior de su
aplicación. Windows la sigue tratando como tal en la barra de tareas, Alt+Tab, escritorios virtuales y Snap. Esta fase
decide cómo se comporta el grupo como una sola unidad.

## Decisión

1. **Un solo botón en la barra de tareas por grupo: el del marco.** Las inactivas ya no lo tienen (están ocultas con
   `SW_HIDE`). El de la activa se quita con `ITaskbarList::DeleteTab`. Explorer lo vuelve a añadir a veces (cambio de
   título, reactivación), así que se reaplica al mostrarla, en cada `FOREGROUND`, en cada `NAMECHANGE` de la activa y en
   la comprobación periódica. Al restaurar una ventana visible se llama a `AddTab`. No se cambian estilos de ventana
   ajenos (`WS_EX_TOOLWINDOW`) porque eso no se puede deshacer con seguridad si TabDock muere: un botón quitado con
   `DeleteTab` reaparece solo cuando la aplicación se reactiva o Explorer se reinicia.
2. **Foco:** si el marco se activa desde fuera (botón de la barra de tareas, Alt+Tab), el foco pasa a la ventana activa
   del grupo. Si la activación viene de un clic dentro del marco (barra lateral), el foco se queda en el marco.
3. **Escritorios virtuales (`IVirtualDesktopManager`):** la API pública solo permite mover ventanas *propias*. El marco
   sigue al escritorio de la ventana activa (eventos `CLOAKED`/`UNCLOAKED` y comprobación periódica). Las pestañas
   ocultas se quedan en el escritorio donde estaban. Si el usuario mueve la activa a otro escritorio (Win+Tab), el marco
   va tras ella.
4. **Snap:** si la ventana activa cambia de tamaño a un rectángulo que toca al menos dos bordes del área de trabajo
   (tolerancia 2 px), se interpreta como Snap Layouts o Win+flechas. El marco entero ocupa esa zona, en lugar de
   devolver la ventana al hueco. El reajuste por cambio de DPI no cumple la condición y se sigue corrigiendo (máximo 3
   veces por hueco).
5. **Ventanas que no responden:** `SetWindowPos` y `ShowWindow` usan `SWP_ASYNCWINDOWPOS` / `ShowWindowAsync` si
   `IsHungAppWindow` es cierto, así que TabDock nunca espera a la aplicación colgada. La pestaña muestra "no responde"
   y vuelve a la normalidad sola cuando la aplicación se recupera.
6. **Ventanas elevadas:** con UIPI un proceso sin elevar no puede moverlas. No se capturan. Si una regla las encuentra,
   se cuentan y se avisa en la bandeja cada vez que se agrupa con esa regla, en vez de fallar en silencio.

## Consecuencias

- Alt+Tab sigue mostrando dos entradas para el grupo: el marco y la ventana activa. Ocultarla de Alt+Tab exige
  `WS_EX_TOOLWINDOW` en una ventana ajena, que se descartó en el punto 1.
- Con los botones combinados (valor por defecto de Windows 11) el botón del grupo lleva el nombre "TabDock"; sin
  combinar, lleva el título del marco ("pestaña — grupo").
- Las pestañas ocultas en otro escritorio virtual no se ven al cambiar a ellas desde TabDock hasta que el marco las
  sigue. Es una limitación de la API pública y queda documentada.
- Aplicaciones sin conciencia de DPI: Windows virtualiza sus coordenadas, pero al colocarlas con `SetWindowPos` desde
  un proceso Per-Monitor V2 quedan ajustadas al hueco en ambos monitores (verificado: 125 % y 100 %).
