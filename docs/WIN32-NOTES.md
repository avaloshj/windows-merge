# Notas Win32 / DWM

> **Estado de verificación:** los valores numéricos no se escriben a mano en el código: se obtienen de los metadatos
> Win32 de `Microsoft.Windows.CsWin32` (0.3.367), que son la fuente de verdad. Las APIs usadas en la Fase 1
> (enumeración, metadatos de ventana y de proceso) están implementadas y cubiertas por pruebas de integración en
> Windows 11 (26200). El resto de filas se verifica en la fase que las implemente.

## Hallazgos de la Fase 8

- **Ecos de las propias acciones:** el `EVENT_OBJECT_SHOW` de una ventana que TabDock mostró puede llegar después de
  que TabDock la vuelva a ocultar al cambiar de pestaña. Tomado como "otro programa la mostró", reactivaba pestañas
  antiguas en cadena (2941 activaciones en un minuto con 20 ventanas y 20 cambios rápidos). Solo se atiende si la
  ventana sigue visible (`IsWindowVisible`). Igual con `EVENT_SYSTEM_FOREGROUND` (`GetForegroundWindow`).
- **`EVENT_OBJECT_DESTROY` llega al empezar `DestroyWindow`:** procesado al instante, `IsWindow` aún es cierto (66 de
  ~100 cierres en una ráfaga). Se confirma a los 250 ms; Windows reutiliza HWND enseguida al crear y cerrar ventanas en
  ráfaga.
- **`WM_HOTKEY` publicado por un script no da derecho a cambiar el primer plano:** `SetForegroundWindow` falla siempre
  en las pruebas automáticas; los avisos de primer plano atrasados quedan mucho más expuestos que con un atajo real.
- **Memoria (conjunto de trabajo / privada):** en reposo 140 / 85 MB; con el marco abierto ~255 / ~170 MB, igual con 1,
  20 o 50 ventanas (el coste es el dispositivo gráfico de WPF y los búferes de una ventana de 2560×1440). Igual en
  Release y con `DOTNET_gcConcurrent=0` y `DOTNET_GCgen0size`. Con `RenderOptions.ProcessRenderMode = SoftwareOnly`
  (modo de bajo consumo): 100 / 45 MB en reposo y ~160 / ~75–88 MB con el marco.
- `RenderOptions.ProcessRenderMode` solo afecta a las ventanas que se crean después: se fija antes de crear la primera.
- Publicación autocontenida con ReadyToRun: ~140 MB (x64) y ~150 MB (arm64).

## Hallazgos de la Fase 7

- **Sombra de la ventana gestionada sobre la barra lateral:** `DwmSetWindowAttribute(DWMWA_NCRENDERING_POLICY,
  DWMNCRP_DISABLED)` funciona con ventanas de otro proceso (`S_OK`; `DWMWA_NCRENDERING_ENABLED` pasa a 0) y se deshace
  con `DWMNCRP_USEWINDOWSTYLE`. Pero solo es seguro si la ventana no tiene **ningún** marco nativo (área de cliente =
  rectángulo de la ventana; `TestTarget --frameless`). VS Code con barra de título propia conserva los bordes de
  redimensionado nativos (invisibles, unos 9 px; área de cliente = área visible; `TestTarget --custom-chrome`): sin el
  dibujo de DWM, Windows pinta en ellos un borde clásico azul claro, peor que la sombra. Por eso VS Code **conserva la
  sombra**. Una región de ventana (`SetWindowRgn`) recortada al área visible tampoco desactiva el dibujo de DWM
  (`NCRENDERING_ENABLED` sigue a 1). La sombra no se puede comprobar sin capturar la pantalla (no permitido con ventanas
  ajenas a la vista). La decisión va en el snapshot (`ShadowSuppressed`) y `Restore` devuelve la sombra en todas las
  rutas.
- **Orden Z del marco** (ADR 0009): el marco va justo **encima** de la ventana activa, con un hueco (`SetWindowRgn`
  sobre la ventana propia) por el que se ve y se hace clic en ella; así tapa su sombra. Cada `WM_WINDOWPOSCHANGED` del
  marco con cambio de orden Z avisa a la sesión, que lo vuelve a poner encima si no lo está (comprobarlo antes evita el
  bucle). `WindowChrome` sin vidrio fija su propia región en cada movimiento: el marco siempre usa vidrio (al menos 1 px).
- **Cambio de pestaña con un clic que activa el marco:** WPF da el botón por suelto durante `WM_ACTIVATE` (aún no ha
  procesado el mensaje del botón), así que el marco creía que lo habían activado desde fuera y devolvía el foco a la
  pestaña anterior. El aviso de primer plano de esa pestaña llegaba después del cambio y lo deshacía. Ahora el clic se
  detecta con `GetAsyncKeyState` y la posición real del cursor, y un aviso de primer plano de una pestaña oculta solo
  la activa si sigue en primer plano (`GetForegroundWindow`). Tras un clic derecho funcionaba porque el marco ya estaba
  activo.
- **Avisos de cierre falsos de Chromium:** VS Code emite `EVENT_OBJECT_DESTROY` (`OBJID_WINDOW`, `CHILDID_SELF`) con el
  HWND de la ventana al ocultarla, sin destruirla. Solo se da por cerrada si `IsWindow` falla
  (`TestTarget --destroy-event-on-hide` lo reproduce). Un clic en la pestaña ya seleccionada vuelve a mostrar su
  ventana y le da el foco.
- **Maximizar desde la ventana gestionada con un clic real:** `WindowState = Maximized` activa el marco y lo sube por
  encima de la ventana, que quedaba tapada. Tras maximizar o restaurar, la sesión vuelve a poner el marco debajo y
  devuelve el foco a la ventana. Con `SW_MAXIMIZE` enviado desde fuera no se reproduce (TabDock no puede activarse sin
  entrada del usuario); `e2e-desktop.ps1` comprueba ahora el orden Z, no solo la visibilidad.

- **System.Text.Json con generación de código y propiedades `init`:** si una propiedad falta en el JSON, el código
  generado le asigna el valor por defecto del tipo (`false`, 0, `null`), no el de su inicializador. Un `settings.json`
  migrado con `"user": { "onboardingCompleted": true }` dejaba Mica desactivado, el intervalo en 0 y el idioma vacío. La
  sección `user` se completa con los valores por defecto antes de deserializar (prueba incluida). `AppRule` y
  `HotkeyBinding` tienen el mismo riesgo si se editan a mano; los archivos que escribe TabDock siempre están completos.
- Un atajo registrado con `RegisterHotKey` no llega nunca a la ventana con el foco: para poder reasignarlo, Ajustes
  libera todos los atajos mientras se escribe uno nuevo y los vuelve a registrar al salir.
- WPF: `{x:Static}` de una constante `int` en una propiedad `double` (`Slider.Minimum`) lanza `XamlParseException` al
  abrir la ventana. Se usan literales.
- Los elementos de un `ComboBox` con `DisplayMemberPath` exponen a UI Automation (y a Narrador) el `ToString()` del
  objeto, no el texto visible: `OptionItem<T>` devuelve su etiqueta.
- `--export-diagnostics` se ejecuta antes de la comprobación de instancia única: funciona con TabDock en marcha.
- La clave `Run` se escribe con la ruta entre comillas (`"C:\...\TabDock.exe"`): sin comillas, una ruta con espacios
  falla al iniciar sesión.

## Hallazgos de la Fase 6

- `ITaskbarList::DeleteTab` necesita `HrInit` una vez por instancia. Funciona en ventanas de otros procesos y se
  deshace solo (Explorer vuelve a añadir el botón al reactivar la ventana), por eso se reaplica. Verificado con UI
  Automation sobre la barra de tareas: con botones combinados, antes del agrupado aparece "TabDock.TestTarget: 3
  ventanas en ejecución" y después solo "TabDock: 1 ventana en ejecución".
- `IVirtualDesktopManager` (Windows 10 1507+): `GetWindowDesktopId` funciona con cualquier ventana;
  `MoveWindowToDesktop` solo admite ventanas del propio proceso (según la documentación; no se
  intenta con ventanas ajenas). Solo se mueve el marco.
- Snap simulado con `SetWindowPos` sobre la ventana activa: el rectángulo de la ventana incluye los bordes invisibles,
  así que la zona se compara con `DWMWA_EXTENDED_FRAME_BOUNDS`. El marco ocupa exactamente la zona (sin diferencia en
  px en la prueba).
- `IsHungAppWindow` pasa a ser cierto unos 5 s después de que el hilo de UI deja de atender mensajes, también con la
  ventana oculta. Con `SWP_ASYNCWINDOWPOS` el `WM_NULL` al marco tarda 0 ms aunque se active la pestaña colgada.
- **Sin conciencia de DPI** (`TestTarget --dpi-unaware`, monitores al 125 % y al 100 %): la ventana queda dentro del
  hueco en los dos monitores. El margen de la barra lateral escala con el marco (246 px → 308 px físicos), sin
  correcciones extra.
- Maximizar la ventana gestionada (`SW_MAXIMIZE` desde fuera, igual que su botón) maximiza el marco y la ventana
  vuelve al hueco sin quedar maximizada. La segunda vez restaura el marco.

## Hallazgos de la Fase 5

- Constantes confirmadas: `DWM_TNP_RECTDESTINATION = 1`, `DWM_TNP_OPACITY = 4`, `DWM_TNP_VISIBLE = 8`,
  `DWM_TNP_SOURCECLIENTAREAONLY = 16`. `DwmRegisterThumbnail` devuelve `S_OK` también con el origen oculto.
- **Cambio de monitor con otra escala** (prueba real: 125 % → 100 %): al colocar la ventana en otro monitor, la
  aplicación recibe `WM_DPICHANGED` y aplica por su cuenta el tamaño sugerido (×0,8) *después* de que TabDock la
  coloque. Le pasa también a VS Code/Electron. La sesión la vuelve a colocar al recibir el `LOCATIONCHANGE`, con un
  máximo de 3 correcciones por hueco (una ventana con tamaño mínimo mayor que el hueco no provoca un bucle). Antes
  había una espera fija de 500 ms que descartaba justo esta corrección y hacía el fallo intermitente.
- Aplicaciones **sin** conciencia de DPI: Windows virtualiza sus coordenadas entre monitores de distinta escala
  (resuelto en la Fase 6, ver arriba). `TestTarget` declara Per-Monitor V2 para representar a VS Code.
- Las ventanas creadas por el propio proceso del terminal de pruebas no aparecían en el escritorio del usuario; las de
  procesos hijos sí: las comprobaciones visuales se hacen siempre con procesos hijos (TabDock, TestTarget).

## Hallazgos de la Fase 4

- `RegisterHotKey` devuelve `ERROR_HOTKEY_ALREADY_REGISTERED` (1409) para `Ctrl+Alt+Tab` y `Ctrl+Alt+Mayús+Tab`
  (reservados por Windows para el selector de tareas persistente). Valores confirmados: `MOD_ALT = 1`,
  `MOD_CONTROL = 2`, `MOD_SHIFT = 4`, `MOD_WIN = 8`, `MOD_NOREPEAT = 0x4000`, `WM_HOTKEY = 0x0312`.
- Los atajos se registran en una ventana de solo mensajes (`HwndSource` con padre `HWND_MESSAGE`): no aparece en
  Alt+Tab ni en la barra de tareas y no recibe difusiones.
- Dos procesos de TabDock (una instancia nueva y el guardián de la anterior) pueden escribir `state.json` a la vez;
  además, indexadores y antivirus lo bloquean un instante. La escritura atómica reintenta (20–320 ms) y el guardián
  solo restaura si `state.json` sigue perteneciendo a su proceso padre (`OwnerProcessId`).

## Hallazgos de la Fase 3

- Constantes confirmadas en CsWin32: `EVENT_OBJECT_CREATE = 0x8000`, `EVENT_OBJECT_UNCLOAKED = 0x8018`,
  `EVENT_SYSTEM_FOREGROUND = 3`, `WINEVENT_OUTOFCONTEXT = 0`, `WINEVENT_SKIPOWNPROCESS = 2`, `WM_CLOSE = 16`,
  `SW_SHOWNOACTIVATE = 4`, `SW_SHOWMINNOACTIVE = 7`, `SW_SHOWMAXIMIZED = 3`.
- `SetWinEventHook` tiene una sobrecarga que devuelve `UnhookWinEventSafeHandle`: liberar el `SafeHandle` llama a
  `UnhookWinEvent`. El delegado del callback debe mantenerse vivo (campo) mientras existan los hooks.
- Con `WINEVENT_OUTOFCONTEXT` los eventos llegan al hilo que instaló el hook, que necesita bucle de mensajes (el de UI;
  en pruebas, un hilo STA con `Dispatcher`).
- `ShowWindow` devuelve la **visibilidad anterior**, no un código de error.
- Para restaurar una ventana maximizada sin activarla: `SetWindowPlacement` con `showCmd = SW_SHOWNOACTIVATE`
  (verificado en integración: queda en estado normal y en el rectángulo pedido). Restaurar maximizada exige
  `SW_SHOWMAXIMIZED`, que sí activa: no existe variante "maximizar sin activar".
- `SetForegroundWindow` falla si TabDock no tiene la última entrada del usuario (p. ej. cambios provocados por
  UI Automation o por otro proceso). Por eso el foco se pide **después** de mostrar la nueva y ocultar la anterior: los
  reintentos (50/100/200 ms) ya no retrasan el cambio visible, que se mide en 10–32 ms.
- `Process.ExitCode` de un proceso obtenido con `Process.GetProcessById` lanzó una excepción tras matarlo; el guardián
  ya no lo lee y cualquier fallo al vigilar no impide la restauración.
- Un proceso sin conciencia de DPI por monitor recibe coordenadas virtualizadas con escala ≠ 100 %: las pruebas de
  integración declaran Per-Monitor V2 en su manifiesto, igual que TabDock.
- `Process.MainWindowHandle` devuelve 0 para ventanas ocultas: los scripts guardan los HWND mientras son visibles.
- Doble botón en la barra de tareas: el marco y la ventana activa (visible) tienen cada uno el suyo; las ocultas no.
  Quitar el de la ventana activa requiere `ITaskbarList::DeleteTab` (Fase 6, junto al modo `taskbarOnly`).

## Hallazgos de la Fase 2

- Valores de CsWin32 confirmados: `DWMWA_USE_IMMERSIVE_DARK_MODE = 20`, `DWMWA_WINDOW_CORNER_PREFERENCE = 33`,
  `DWMWA_SYSTEMBACKDROP_TYPE = 38`, `DWMSBT_MAINWINDOW = 2`, `DWMSBT_TRANSIENTWINDOW = 3`, `DWMWCP_ROUND = 2`.
- **Mica en WPF** necesita `WindowChrome.GlassFrameThickness = -1`, fondo de ventana transparente y
  `HwndSource.CompositionTarget.BackgroundColor = Transparent`. Hay que poner `UseAeroCaptionButtons = false`: si no,
  DWM dibuja sus propios botones de ventana sobre el marco extendido.
- **Maximizar con `WindowChrome`**: la ventana sobresale de la pantalla el grosor de sus bordes de redimensionado. Se
  compensa con un margen igual a la diferencia entre `GetWindowRect` y `MONITORINFO.rcWork` (o `rcMonitor` en
  pantalla completa).
- **Tema del sistema:** no hay API Win32 pública; se lee `HKCU\…\Themes\Personalize\AppsUseLightTheme` (lo que escribe
  Configuración) y se escucha `SystemEvents.UserPreferenceChanged`. La alternativa documentada es WinRT `UISettings`,
  que obligaría a un TFM con proyecciones WinRT.
- Los generadores de código fuente no ven la salida de otros generadores: un `[LoggerMessage]` no puede recibir
  tipos generados por CsWin32 (se registran como `int`).
- **Capturas para revisión visual:** `PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT)` renderiza solo la ventana propia,
  aunque esté tapada, y nunca captura contenido de otras ventanas. Se descartó copiar de la pantalla
  (`CopyFromScreen`) porque puede incluir ventanas ajenas superpuestas.

## Hallazgos de la Fase 1

- WPF (y en general cualquier app) crea la ventana y le asigna título **antes** de mostrarla: `EnumWindows` la devuelve
  ya con título pero `IsWindowVisible = false`. Confirma la necesidad del debounce de auto-merge (Fase 4).
- Hay rutas de ejecutable con el nombre de usuario **fuera** del perfil (p. ej. `C:\ProgramData\<usuario>\…`). El
  enmascaramiento sustituye cualquier segmento de ruta igual al nombre de usuario por `%USERNAME%`.
- En un equipo de desarrollo típico `EnumWindows` devuelve cientos de ventanas (≈800 en la prueba manual), de las que
  solo unas decenas son elegibles; la enumeración completa con metadatos tarda ≈45 ms.
- Para estilos (`GWL_STYLE`, `GWL_EXSTYLE`) se usa `GetWindowLong`: los estilos caben en 32 bits y evita el
  problema de `GetWindowLongPtr` en compilaciones AnyCPU de CsWin32.

## APIs y versión mínima

| API | DLL | Mínimo cliente | Uso en TabDock | Notas / limitaciones |
|---|---|---|---|---|
| `EnumWindows` | user32 | 2000 | Enumerar ventanas de primer nivel | No enumera ventanas hijas. |
| `GetWindowThreadProcessId` | user32 | 2000 | PID del propietario | |
| `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` + `QueryFullProcessImageNameW` | kernel32 | Vista | Ruta del ejecutable | Falla con acceso denegado en procesos protegidos; los elevados sí permiten `QUERY_LIMITED`. |
| `GetProcessTimes` | kernel32 | 2000 | `ProcessStartTime` para identidad | |
| `OpenProcessToken` + `GetTokenInformation(TokenElevation)` | advapi32 | Vista | Detectar proceso elevado | Puede fallar en elevados → tratar como elevado. |
| `GetWindowTextW` / `GetWindowTextLengthW` | user32 | 2000 | Título | **Corrección de Fase 0:** para ventanas de *otro* proceso no envía `WM_GETTEXT`, lee el texto interno, así que no se bloquea aunque la ventana no responda. Solo envía el mensaje a ventanas del propio proceso. |
| `GetClassNameW` | user32 | 2000 | Clase (`Chrome_WidgetWin_1`, `#32770`) | |
| `GetWindowLongW(GWL_STYLE/GWL_EXSTYLE)` | user32 | 2000 | `WS_EX_TOOLWINDOW`, `WS_CAPTION` | Los estilos son de 32 bits; no hace falta la variante `Ptr`. |
| `SetWindowLongPtrW` | user32 | 2000 | **Solo** opción experimental de quitar caption | Sobre ventanas ajenas, siempre revertido. Requiere `SetWindowPos(SWP_FRAMECHANGED)`. |
| `IsWindow`, `IsWindowVisible`, `IsIconic`, `IsZoomed` | user32 | 2000 | Validez y estado | |
| `IsHungAppWindow` | user32 | 2000 | Estado `unresponsive` | |
| `GetWindow(GW_OWNER)`, `GetAncestor(GA_ROOTOWNER)` | user32 | 2000 | Excluir ventanas con propietario | |
| `GetWindowRect` | user32 | 2000 | Rect con bordes invisibles | |
| `SetWindowPos`, `BeginDeferWindowPos`/`DeferWindowPos`/`EndDeferWindowPos` | user32 | 2000 | Colocar en el slot, orden Z | Sobre ventanas de otros procesos funciona (no elevadas). Usar `SWP_NOACTIVATE`, `SWP_ASYNCWINDOWPOS` para no bloquear si la ventana cuelga. |
| `ShowWindow` / `ShowWindowAsync` | user32 | 2000 | `SW_HIDE`, `SW_SHOWNA`, `SW_RESTORE` | Preferir `ShowWindowAsync` para ventanas ajenas (no bloquea si no responde). |
| `GetWindowPlacement` / `SetWindowPlacement` | user32 | 2000 | Snapshot y restauración | `rcNormalPosition` está en **coordenadas de área de trabajo**, no de pantalla. |
| `SetForegroundWindow` | user32 | 2000 | Dar foco a la ventana activa | Solo tiene éxito si el proceso llamante es el de primer plano, recibió la última entrada, etc. (ver "Remarks" en Learn). |
| `AllowSetForegroundWindow` | user32 | 2000 | Evaluar en Fase 3 | |
| `SetWinEventHook` / `UnhookWinEvent` | user32 | 2000 | Eventos de ventana | `WINEVENT_OUTOFCONTEXT` → callback en el hilo que instaló el hook, requiere bucle de mensajes. `WINEVENT_SKIPOWNPROCESS` para ignorar el propio marco. |
| `PostMessageW(WM_CLOSE)` | user32 | 2000 | Cerrar ventana desde pestaña | `PostMessage`, no `SendMessage` (no bloquear). |
| `RegisterHotKey` / `UnregisterHotKey` | user32 | 2000 | Atajos globales | Falla con `ERROR_HOTKEY_ALREADY_REGISTERED` (1409) si hay conflicto. `MOD_NOREPEAT` desde Win7. |
| `MonitorFromWindow`, `GetMonitorInfoW` | user32 | 2000 | Monitor del marco / `szDevice` | |
| `GetDpiForWindow` | user32 | **Win10 1607** | DPI por ventana | |
| `IVirtualDesktopManager` | shell32 (COM) | Win10 | `IsWindowOnCurrentVirtualDesktop`, `GetWindowDesktopId`, `MoveWindowToDesktop` | `MoveWindowToDesktop` solo funciona con ventanas **del propio proceso** → se mueve el marco, no la ventana ajena. |
| `ITaskbarList` (`HrInit`, `AddTab`, `DeleteTab`) / `ITaskbarList3` | shell32 (COM) | XP / Win7 | Modo `taskbarOnly` | Explorer puede re-añadir el botón; re-aplicar tras `RegisterWindowMessage("TaskbarCreated")`. |
| `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` | dwmapi | Vista | Compensar bordes invisibles | Devuelve píxeles físicos; el proceso debe ser PMv2 para obtener valores consistentes. |
| `DwmGetWindowAttribute(DWMWA_CLOAKED)` | dwmapi | Win8 | Excluir ventanas en otros escritorios / UWP suspendidas | Valor ≠ 0 ⇒ cloaked (`DWM_CLOAKED_APP`, `_SHELL`, `_INHERITED`). |
| `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)` | dwmapi | **Win11** (documentado) | Chrome oscuro del marco | En Win10 20H1+ el atributo existe pero no está documentado; solo usarlo en Win10 tras probarlo, con fallo silencioso. |
| `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND)` | dwmapi | **Win11 (22000)** | Esquinas 12 px aprox. del sistema | En Win10 devuelve error → ignorar. |
| `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_MAINWINDOW/TRANSIENTWINDOW)` | dwmapi | **Win11 22H2 (22621)** | Mica / Acrylic | En 22000 no existe → fondo sólido. WPF necesita fondo transparente en la raíz + `DwmExtendFrameIntoClientArea`. |
| `DwmExtendFrameIntoClientArea` | dwmapi | Vista | Requisito de Mica con chrome propio | |
| `DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties` / `DwmUnregisterThumbnail` | dwmapi | Vista | Vista previa en vivo | El destino debe ser una ventana de primer nivel del propio proceso (popup de previsualización). Nunca se guarda en disco. |
| `SystemParametersInfo(SPI_GETCLIENTAREAANIMATION)` | user32 | Vista | Reducir animaciones | En WPF: `SystemParameters.ClientAreaAnimation`. |

## Snap Layouts con chrome personalizado (Windows 11)

El menú de Snap Layouts aparece al pasar sobre el botón maximizar cuando la ventana responde a `WM_NCHITTEST`
con `HTMAXBUTTON` en esa zona. Con `WindowChrome` de WPF esto **no** ocurre automáticamente: hay que añadir un
`HwndSource` hook que devuelva `HTMAXBUTTON` sobre el botón verde y gestionar `WM_NCMOUSELEAVE`/`WM_NCLBUTTONDOWN`
para que el botón siga funcionando. A validar en Fase 6.

## Bordes invisibles

En Windows 10/11 las ventanas con `WS_THICKFRAME` tienen un borde de redimensionado invisible (≈7 px a 96 DPI,
escala con el DPI). Para que el área visible coincida con el slot:

```
offset = GetWindowRect(hwnd) - DWMWA_EXTENDED_FRAME_BOUNDS(hwnd)   // por cada lado
SetWindowPos(hwnd, slot.Left + offset.Left, slot.Top + offset.Top,
             slot.Width + offset.Left + offset.Right, slot.Height + offset.Top + offset.Bottom)
```

El offset se recalcula tras cada cambio de DPI de la ventana gestionada.

## VS Code

- Proceso: todas las ventanas pertenecen al proceso principal de Electron (`Code.exe`), clase
  `Chrome_WidgetWin_1`. Los procesos hijos (renderer, GPU, extension host) no tienen ventanas de primer nivel
  visibles. **Verificar en Fase 1 con la ventana de diagnóstico.**
- Título por defecto (`window.title`): `${dirty}${activeEditorShort}${separator}${rootName}${separator}${profileName}${separator}${appName}`,
  con `${separator}` = `" - "`. Verificar en la documentación vigente de VS Code en Fase 4.
- `window.titleBarStyle` en Windows ya es `custom` por defecto. **Pendiente:** confirmar qué ajustes existen hoy
  para reducir los controles propios de VS Code; no se asume ninguno.
