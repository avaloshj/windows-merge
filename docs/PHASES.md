# Historial por fases

Qué se construyó en cada fase, qué se decidió y cómo comprobarlo. El detalle técnico está en los documentos enlazados:
[ARCHITECTURE](ARCHITECTURE.md), [DESIGN](DESIGN.md), [WIN32-NOTES](WIN32-NOTES.md), [TEST-PLAN](TEST-PLAN.md) y los
ADR de [DECISIONS](DECISIONS/).

Todos los datos de ejemplo y de prueba son ficticios (`proyecto-alfa`, `proyecto-beta`, `demo-api`, `panel-lento`).

## Resumen

| Fase | Tema | ADR | Prueba de extremo a extremo |
|---|---|---|---|
| 0 | Planificación | 0001, 0002, 0003 | — |
| 1 | Base y diagnóstico | — | — |
| 2 | Marco anfitrión y sistema visual | 0004 | — |
| 3 | Motor de captura y restauración | 0002, 0003 | `e2e-recovery.ps1`, `e2e-sync.ps1` |
| 4 | Merge All Windows, reglas, atajos y bandeja | 0005 | `e2e-rules.ps1` |
| 5 | Funciones de pestaña | 0006 | `e2e-tabs.ps1` |
| 6 | Robustez de escritorio | 0007 | `e2e-desktop.ps1` |
| 7 | Configuración y onboarding | 0008 | `e2e-settings.ps1` |
| 8 | Endurecimiento, pruebas y distribución | 0009, 0010, 0011 | `e2e-stress.ps1` |

Comandos comunes:

```powershell
dotnet build TabDock.sln
dotnet test --solution TabDock.sln
dotnet format TabDock.sln --verify-no-changes
dotnet run --project src/TabDock.App            # opciones: --settings, --onboarding, --lang en, --theme dark, --diagnostics, --verbose
powershell -ExecutionPolicy Bypass -File build\e2e-<nombre>.ps1
```

Los scripts `build/e2e-*.ps1` solo usan ventanas `TabDock.TestTarget`, hacen copia de `settings.json` y
`preferences.json` y los restauran al terminar, y prueban los atajos publicando `WM_HOTKEY` (no simulan teclas).

---

## Fase 0 — Planificación (sin código)

**Entregado:** arquitectura por capas (`Core` sin Win32/WPF, `Win32`, `Infrastructure`, `App`), diagrama de
componentes, contratos `IWindowSystem` / `IWindowEvents` / `IHostFrame`, modelo de datos, máquina de estados de
`ManagedWindow`, lista de APIs Win32 con su versión mínima de Windows, riesgos (UIPI, foco, DPI, escritorios virtuales)
y plan de pruebas.

**Decisiones:**

- [ADR 0001](DECISIONS/0001-superposicion-sincronizada-vs-reparentado.md): superposición sincronizada. La ventana
  gestionada sigue siendo de nivel superior y se coloca encima del hueco del marco; nunca `SetParent` sobre ventanas
  ajenas.
- [ADR 0002](DECISIONS/0002-modo-de-ocultacion.md): las pestañas inactivas se ocultan con `SW_HIDE` (modo `hide`,
  por defecto).
- [ADR 0003](DECISIONS/0003-proceso-guardian.md): proceso guardián que restaura las ventanas si TabDock muere.

## Fase 1 — Base y diagnóstico

**Entregado:**

- Solución .NET 10 / C# 14 con `global.json`, `Directory.Build.props` (nullable, `TreatWarningsAsErrors`, analizadores
  Roslynator y Meziantou) y gestión central de paquetes.
- DI con `Microsoft.Extensions.Hosting`; Win32 solo mediante CsWin32 (sin `DllImport` manuales en el producto).
- Serilog con enmascaramiento obligatorio: títulos, rutas y nombres de proyecto se registran como hash de 8
  caracteres + longitud; rutas de usuario normalizadas a `%USERPROFILE%`; también en trazas de excepción.
- `TabDock.TestTarget`: ventana WPF mínima para reproducir casos (título, tamaño, ventana de herramientas, diálogo con
  propietario, maximizada).
- Ventana de diagnóstico (`--diagnostics`): ventanas elegibles con ejecutable, clase y motivo de exclusión.

**Hallazgos clave:** las ventanas reciben título antes de ser visibles; hay rutas con el nombre de usuario fuera del
perfil (se enmascara cualquier segmento igual al usuario).

## Fase 2 — Marco anfitrión y sistema visual

**Entregado:** `HostFrameWindow` con `WindowChrome`, barra lateral de pestañas verticales, Mica y esquinas redondeadas
(Windows 11; fondo sólido en Windows 10), tokens claro/oscuro/alto contraste, tokens de movimiento con variante de
animaciones reducidas, fuente Inter (OFL) e iconos Lucide (ISC), español por defecto e inglés.

**Decisiones:**

- [ADR 0004](DECISIONS/0004-botones-de-ventana-de-windows.md): por petición del usuario se usan los botones de ventana
  de Windows (minimizar, maximizar, cerrar) en lugar del semáforo de macOS. Revisión posterior ("modelo Groupy"): la
  barra de título de la aplicación gestionada hace de barra del grupo, para no duplicar botones.

**Hallazgos clave:** Mica en WPF exige `GlassFrameThickness = -1` y `UseAeroCaptionButtons = false`; al maximizar con
`WindowChrome` hay que compensar el borde que sobresale.

## Fase 3 — Motor de captura y restauración

**Entregado:**

- `WinEventListener` (`SetWinEventHook` fuera de contexto) que alimenta un `Channel` consumido en el hilo de UI.
- `TabGroupSession`: capturar, mostrar en el hueco (compensando bordes invisibles con `DWMWA_EXTENDED_FRAME_BOUNDS`),
  ocultar, sincronizar posición y tamaño, minimizar con el marco, cerrar pestañas.
- Snapshots (`WINDOWPLACEMENT`) guardados en `state.json` **antes** de mover ninguna ventana, con escritura atómica y
  reintentos.
- Restauración al salir, en `SessionEnding`, por el guardián (`--guardian`) y en el siguiente arranque.

**Pruebas:** `e2e-recovery.ps1` (matar TabDock, cerrar el marco, matar TabDock y guardián → posición original exacta y
`state.json` limpio); `e2e-sync.ps1` (cambiar de pestaña, mover y redimensionar, minimizar, cierre de ventanas).

**Hallazgos clave:** el foco se pide después del cambio visible (cambio de pestaña en 10–32 ms); `ShowWindow` devuelve
la visibilidad anterior, no un error.

## Fase 4 — Merge All Windows, reglas, atajos y bandeja

**Entregado:**

- `AppRule`, `RuleMatcher`, `EligibilityPolicy`, `GroupingService` (idempotente) y agrupado automático con debounce de
  150 ms que solo añade a un grupo existente.
- `VsCodeTitleParser` (separador, plantillas, remotos SSH/WSL/Dev Container, sin carpeta, marcadores finales) y
  `GenericTitleParser`.
- Atajos globales en una ventana de solo mensajes: `Ctrl+Alt+M` (Merge All), `Ctrl+Alt+U` (separar),
  `Ctrl+Alt+AvPág/RePág` (siguiente/anterior), `Ctrl+Alt+B` (barra lateral), `Ctrl+Alt+1…9`.
- Icono de bandeja, TabDock residente, instancia única, `MergeOnStartup` por regla.

**Decisiones:** [ADR 0005](DECISIONS/0005-reglas-y-agrupado-automatico.md). `Ctrl+Alt+Tab` está reservado por Windows
(error 1409), por eso siguiente/anterior usan AvPág/RePág.

**Pruebas:** `e2e-rules.ps1`.

## Fase 5 — Funciones de pestaña

**Entregado:** reordenar arrastrando, fijar, alias (doble clic o F2), colores, desacoplar arrastrando fuera, mover a
otro grupo, menú contextual, vista previa en vivo (`DwmRegisterThumbnail`, nunca se guarda) con tarjeta si la ventana
está oculta, búsqueda rápida difusa (`Ctrl+Alt+P`) y `preferences.json` por `(regla, proyecto)` con purga a 90 días.

**Decisiones:** [ADR 0006](DECISIONS/0006-pestanas-preferencias-y-vista-previa.md). Se descartó `PrintWindow` sobre
ventanas ajenas: la política no permite leer su contenido.

**Pruebas:** `e2e-tabs.ps1`; arrastrar, menú contextual y vista previa se comprueban a mano (TEST-PLAN).

**Hallazgos clave:** en monitores con distinta escala la aplicación se redimensiona sola tras `WM_DPICHANGED`; la
sesión corrige hasta 3 veces por hueco.

## Fase 6 — Robustez de escritorio

**Entregado:**

- Un solo botón en la barra de tareas por grupo (`ITaskbarList::DeleteTab` sobre la ventana activa, reaplicado).
- Maximizar desde la ventana gestionada alterna el marco; minimizar minimiza el grupo.
- Snap Layouts / Win+flechas sobre la ventana activa mueve el grupo entero.
- El foco pasa a la ventana activa cuando el marco se activa desde la barra de tareas o Alt+Tab.
- El marco sigue al escritorio virtual de la ventana activa.
- Ventanas que no responden: operaciones asíncronas y aviso "no responde" en la pestaña.
- Ventanas elevadas: no se capturan; aviso en la bandeja.
- `TestTarget --hang-after <ms>` y `--dpi-unaware`.

**Decisiones:** [ADR 0007](DECISIONS/0007-integracion-con-el-escritorio.md).

**Pruebas:** `e2e-desktop.ps1` (barra de tareas, maximizar, Snap, pestaña colgada sin bloquear TabDock).

**Limitaciones conocidas:** Alt+Tab muestra el marco y la ventana activa; las pestañas ocultas no cambian de
escritorio virtual (la API pública solo mueve ventanas propias).

## Fase 7 — Configuración y onboarding

**Entregado:**

- Ventana de ajustes (bandeja → Configuración…, o `--settings`) con seis secciones: General (idioma, iniciar con
  Windows, volver a ver la introducción), Programas (activar, agrupar ventanas nuevas, agrupar al iniciar, cómo leer el
  título, añadir un programa abierto, quitar), Apariencia (tema, Mica, densidad), Atajos (reasignar pulsando la
  combinación, activar o desactivar, restablecer), Avanzado (modo de ocultación, intervalo de comprobación) y
  Diagnóstico (exportar, logs, ventanas detectadas). Cada cambio se guarda y se aplica al momento; el idioma, al
  reiniciar.
- Introducción de 3 pasos en el primer arranque: programas → ajuste recomendado de VS Code (para copiar) → probar
  `Ctrl+Alt+M`.
- Iniciar con Windows en la clave `Run` del usuario, sin administrador.
- Diagnóstico anonimizado en ZIP (también `--export-diagnostics <ruta>`).
- `settings.json` esquema 2 con migración desde el esquema 1.

**Decisiones:** [ADR 0008](DECISIONS/0008-configuracion-e-introduccion.md). No se ofrece quitar la barra de título de
programas ajenos (contradice la ADR 0004).

**Pruebas:** `e2e-settings.ps1`; la captura de atajos y el diálogo de guardar se comprueban a mano (TEST-PLAN).

**Hallazgos clave:** la deserialización generada no respeta los inicializadores de propiedades `init` ausentes en el
JSON; un atajo registrado no llega a la ventana con el foco.

**Limitaciones conocidas:** barra lateral a la derecha y modo `taskbarOnly` siguen pendientes (Fase 8); un error al
crear una ventana termina TabDock (tras restaurar las ventanas).

## Fase 8 — Endurecimiento, pruebas y distribución

**Entregado:**

- **Uso real con VS Code:**
  - El marco va encima de la ventana con un hueco y tapa su sombra y la línea del borde (ADR 0009).
  - El cambio de pestaña con un clic que activa el marco ya no vuelve a la pestaña anterior.
  - Los avisos de cierre falsos de Chromium ya no sacan ventanas del grupo.
- **Robustez:**
  - Los eventos atrasados (ecos de las propias acciones) se comprueban contra el estado actual; corrige un bucle de
    activaciones con cambios rápidos.
  - Valores por defecto completos al leer `settings.json`.
  - Una ventana que falla al abrirse ya no cierra TabDock.
- **Rendimiento:** prueba de estrés con 20/50 ventanas y una ráfaga de 100; medidas de arranque, CPU, memoria y
  latencia; modo de bajo consumo de memoria.
- **Animaciones reducidas:** prueba automática de los tokens frente a la especificación.
- **Distribución:**
  - `build/publish.ps1`: autocontenido, ReadyToRun, x64 y arm64, firma con un certificado indicado por variables de entorno y paquete de Velopack.
  - Velopack para instalar y desinstalar (restaura las ventanas al desinstalar; sin buscar actualizaciones).
- **Documentación:** `USER-GUIDE.md`, `ARCHITECTURE.md` al día y este historial.

**Decisiones:** [ADR 0009](DECISIONS/0009-marco-encima-con-hueco.md),
[ADR 0010](DECISIONS/0010-endurecimiento-y-distribucion.md),
[ADR 0011](DECISIONS/0011-barra-nativa-oculta.md) (barra de título nativa oculta bajo la del marco).

**Pruebas:** `e2e-stress.ps1` (también con `-LowMemory`) y los demás scripts.

**Limitaciones conocidas:**
- Con aceleración gráfica la memoria supera los 120 MB (coste fijo del marco); el objetivo se cumple en modo de bajo
  consumo.
- El cambio de pestaña tiene picos ocasionales de 60–75 ms (media 20–35 ms).
- Mica y las esquinas redondeadas no se ven mientras el marco tiene ventanas.
- La barra lateral a la derecha y el modo `taskbarOnly` siguen sin implementar.
- La firma y el paquete de Velopack solo se han preparado, no ejecutado (necesitan un certificado y la herramienta
  `vpk`).
