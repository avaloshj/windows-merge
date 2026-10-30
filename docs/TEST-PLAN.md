# Plan de pruebas

Todos los datos de prueba son ficticios (`proyecto-alfa`, `proyecto-beta`, `demo-api`, `cliente-ficticio`).

## 1. Unitarias — `TabDock.Core.Tests` (xUnit v3 + Shouldly + NSubstitute + Verify)

| Área | Casos mínimos |
|---|---|
| `VsCodeTitleParser` | título por defecto; prefijo `●`; sin archivo activo; sin carpeta ("Ventana vacía N"); perfil no predeterminado; `[SSH: demo-host]`, `[WSL: Ubuntu]`, `[Dev Container: demo-api]`; separador personalizado; plantilla recomendada; nombre de proyecto con `-` dentro; Insiders/VSCodium/Cursor (`appName` distinto); títulos vacíos o muy largos; Unicode (tildes, emoji, RTL). Snapshots con `Verify`. |
| `GenericTitleParser` | último segmento como app, resto como nombre; sin separador. |
| `RuleMatcher` / `EligibilityPolicy` | coincidencia por nombre y patrón de ruta (mayúsculas/minúsculas); clase; exclusiones (`#32770`, propietario, `WS_EX_TOOLWINDOW`, cloaked, tamaño < 300×200, título vacío, elevada → "deshabilitada"); cada exclusión devuelve su motivo. |
| `ManagedWindow` | todas las transiciones válidas; transiciones inválidas → `Result.Fail`; invariantes del ADR (snapshot antes de `Attaching`, una sola `Active`). Pruebas basadas en propiedades opcionales (FsCheck) sobre secuencias de eventos. |
| `GroupingService` | idempotencia de MergeAll; auto-merge con debounce (`IClock` falso); MRU tras cierre; grupo vacío cierra marco; orden por `TabPreference` y pinned; error Win32 a mitad de merge restaura las ya movidas. |
| `TabOrdering` | MRU vs orden visual; `Ctrl+Alt+1…9` fuera de rango. |
| Color determinista | mismo `ProjectKey` → mismo color; distribución razonable. |

## 2. Infraestructura — `TabDock.Core.Tests` / `TabDock.Infrastructure.Tests`

- `JsonStateStore`: ida y vuelta; escritura atómica (simular fallo entre tmp y replace); archivo corrupto →
  `.corrupt-<timestamp>` + defaults; campos desconocidos rechazados.
- `SchemaMigrator`: cada migración v(n)→v(n+1) con snapshot; cadena completa desde v1.
- Purga de preferencias: marcado a 90 días, eliminación en el siguiente arranque.
- **Enmascaramiento de logs:** un sink en memoria verifica que títulos, rutas y nombres de proyecto aparecen solo
  como `hash8`+longitud; rutas de usuario normalizadas a `%USERPROFILE%`; prueba negativa que busca las cadenas
  ficticias en la salida y debe no encontrarlas.

## 3. Integración — `TabDock.Win32.IntegrationTests` (Windows real, `TabDock.TestTarget`)

`TabDock.TestTarget` es una app WPF mínima que acepta `--title`, `--size`, `--dirty`, `--owner-dialog`,
`--hang-after <ms>`, `--tool-window` para simular casos.

- Enumeración y elegibilidad sobre instancias reales con cada variante.
- `CaptureSnapshot` → mover/ocultar → `Restore` deja placement idéntico (normal y maximizada).
- `PlaceInSlot` compensa bordes: el área visible coincide con el slot ±0 px a 100 % y 150 %.
- Eventos: crear, cambiar título, cerrar → llegan al canal en orden y con debounce.
- **Recuperación:** se mata el proceso de TabDock (`Process.Kill`) con ventanas ocultas → el guardián las muestra;
  sin guardián → el siguiente arranque las restaura.
- HWND reutilizado: snapshot de un proceso cerrado no se aplica a otra ventana.
- Ventana colgada (`--hang-after`): TabDock no se bloquea (`ShowWindowAsync`, `SWP_ASYNCWINDOWPOS`).

Se ejecutan en local (necesitan un escritorio interactivo) y están marcadas con `[Trait("Category","Integration")]`.

## 3.b Extremo a extremo — `build/e2e-*.ps1` (manual o en un escritorio interactivo)

- `build/e2e-recovery.ps1`: agrupar 3 TestTarget y (A) matar TabDock → restaura el guardián; (B) cerrar el marco con su
  botón → restauración normal; (C) matar TabDock y guardián → restaura el siguiente arranque. En los tres casos se
  comprueba posición original exacta y `state.json` limpio.
- `build/e2e-sync.ps1`: pestañas, cambio de pestaña por UI Automation, mover/redimensionar y minimizar el marco,
  ventanas que se cierran solas y cierre automático del marco vacío.
- `build/e2e-rules.ps1`: TabDock residente, `Ctrl+Alt+M` con regla (idempotente), agrupado automático de una ventana
  nueva como activa, `Ctrl+Alt+1`, `AvPág`/`RePág`, `Ctrl+Alt+U` y nombres de pestaña por proyecto. Usa una
  configuración temporal con solo la regla de TestTarget (copia y restaura `settings.json`) y publica `WM_HOTKEY` en la
  ventana de mensajes de TabDock en lugar de simular teclas.
- `build/e2e-tabs.ps1`: preferencias de la sesión anterior (alias, fijada arriba, orden guardado) y búsqueda rápida
  (se abre con su atajo, filtra de forma difusa con texto escrito por UI Automation). Copia y restaura
  `settings.json` y `preferences.json`.
- `build/e2e-desktop.ps1` (Fase 6): un solo botón en la barra de tareas por grupo (se leen por UI Automation solo los
  botones de TestTarget/TabDock), maximizar desde la ventana gestionada alterna el marco, Snap de la ventana activa a
  media pantalla mueve el marco, y una pestaña colgada (`TestTarget --hang-after`) muestra "no responde" sin bloquear
  TabDock (`WM_NULL` con `SendMessageTimeout` < 500 ms mientras se recorren las pestañas).
- `build/e2e-settings.ps1` (Fase 7): primer arranque con introducción (avanzar y saltar), migración de un
  `settings.json` v1 sin volver a mostrar la introducción, ventana de ajustes con `--settings` (tema, densidad, Mica,
  programas, idioma con aviso de reinicio), inicio con Windows en la clave `Run` (con copia y restauración del valor
  previo) y `--export-diagnostics` con TabDock en marcha: contenido del ZIP sin usuario, equipo, alias ni nombres de
  proyecto.
- `build/e2e-stress.ps1` (Fase 8): arranque, 20 y 50 ventanas (en un solo proceso, como VS Code: `TestTarget
  --windows N`), memoria, CPU en reposo, latencia del cambio de pestaña (log `--verbose`), ráfaga de 100 aperturas y
  cierres en 10 s (`--burst 100 --burst-ms 100`), sin pestañas fantasma y sin ventanas ocultas tras separar. Con
  `-LowMemory` comprueba el objetivo de memoria en modo de bajo consumo.
- Todos los scripts limpian en `finally` (TabDock y TestTarget) aunque fallen a mitad.
- `build/e2e-common.ps1` protege los datos reales: restaura una copia `.e2e-backup` de una prueba interrumpida en vez
  de sobrescribirla, no borra un `state.json` con ventanas vivas y no cierra un TabDock ajeno. **No cortar la salida de
  los scripts con `Select-Object -First`**: PowerShell los detiene sin ejecutar su limpieza.
- Si ya hay un TabDock en marcha, los scripts se detienen sin tocarlo: cerrarlo a la fuerza dejaría ocultas las
  ventanas reales que tuviera agrupadas (su guardián caería con él). Se cierra desde la bandeja → Salir.

### Comprobación manual de la Fase 8

- Instalar el paquete de Velopack (`build\publish.ps1 -Pack`), agrupar ventanas, desinstalar → las ventanas vuelven a
  su sitio y desaparece el inicio con Windows.
- Ajustes → Avanzado → modo de bajo consumo, reiniciar → el marco funciona igual, sin Mica.
- Cambiar rápido de pestaña con clics y atajos reales con varias ventanas de VS Code → nunca vuelve sola a otra
  pestaña.

### Comprobación manual de la Fase 7 (requiere teclado y ratón)

- Ajustes → Atajos: hacer clic en un atajo y pulsar una combinación nueva → se guarda y funciona al momento; una
  combinación de otro comando muestra "Ya lo usa…"; `Ctrl+Alt+Tab` muestra "Windows reserva esta combinación"; Esc sale
  sin cambiar nada; Tab sigue moviendo el foco.
- Ajustes → Diagnóstico → Exportar: diálogo de guardar en el Escritorio con nombre `tabdock-diagnostico-<fecha>.zip`.
- Introducción, paso 2: "Copiar" pone el texto en el portapapeles; paso 3: con dos ventanas de VS Code (carpetas
  ficticias), `Ctrl+Alt+M` o "Agrupar ahora" → "¡Listo!".
- Cambiar el tema y la densidad con un marco abierto → el marco cambia sin reiniciar.
- Cambiar el idioma a inglés y reiniciar TabDock → toda la interfaz en inglés.

### Comprobación manual de la Fase 6

- Clic en el botón del grupo en la barra de tareas y Alt+Tab al marco → el cursor queda en la ventana activa.
- Win+Tab: mover la ventana activa a otro escritorio virtual → el marco la sigue.
- Snap Layouts real (pasar el ratón por maximizar en la ventana gestionada) → el grupo entero ocupa la zona.
- Aplicación elevada que coincide con una regla → aviso en la bandeja, sin captura.
- `TestTarget --dpi-unaware` en monitores con escalas distintas → la ventana encaja en el hueco.

### Comprobación manual de la Fase 5 (requiere ratón)

- Arrastrar una pestaña para reordenarla; soltar fuera de la barra → la ventana queda independiente bajo el cursor;
  Esc durante el arrastre → no pasa nada.
- Menú contextual: renombrar (también doble clic o F2), cambiar color, fijar/desfijar, desacoplar, mover a otro grupo,
  cerrar y cerrar las demás.
- Vista previa tras 400 ms de hover sobre una pestaña inactiva (tarjeta; imagen en vivo si la ventana es visible).
- Reiniciar TabDock y comprobar que alias, colores, fijadas y orden se conservan.

## 4. App — `TabDock.App.Tests`

- ViewModels (sin ventanas): pestañas, renombrar, colapsar barra, estados vacío/error.
- Recursos i18n: todas las claves existen en `es` y `en`.
- Tokens: contraste WCAG AA de pares texto/fondo en ambos temas (cálculo sobre los valores de `Tokens.xaml`).

## 5. Manuales (checklist por fase)

- VS Code real con 3–5 ventanas (carpetas ficticias), incluyendo una remota WSL si está disponible.
- Multi-monitor con escalas distintas; mover el marco entre monitores.
- Escritorios virtuales; Alt+Tab; barra de tareas; Snap Layouts (Win11).
- Windows 10 22H2 (fondo sólido, sin esquinas) y Windows 11.
- Alto contraste, navegación solo teclado, Narrador.
- Reducir animaciones activado.

## 6. Rendimiento y estrés (Fase 8)

- 50 ventanas TestTarget; abrir/cerrar en ráfaga (100 en 10 s) sin pestañas fantasma ni ventanas ocultas.
- Latencia de cambio de pestaña < 50 ms (marca de tiempo del clic → `EVENT_SYSTEM_FOREGROUND` de destino).
- CPU en reposo < 1 %, memoria < 120 MB con 20 ventanas, arranque < 1,5 s.
