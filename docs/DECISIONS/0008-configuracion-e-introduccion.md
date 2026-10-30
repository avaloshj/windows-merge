# ADR 0008 — Configuración, introducción, inicio con Windows y diagnóstico

- **Estado:** Aceptado (2026-10-30)
- **Ámbito:** `SettingsService`, `SettingsMigrator`, `SettingsWindow`, `OnboardingWindow`, `StartupRegistration`,
  `DiagnosticsExporter`

## Decisión

1. **Una sola fuente de configuración en ejecución:** `SettingsService` guarda cada cambio en el acto (no hay botón
   "Aplicar") y lo notifica. `SettingsApplier` lo aplica en caliente: atajos, menú de la bandeja, tema, Mica, densidad e
   intervalo de comprobación. El idioma se aplica al reiniciar, porque los textos ya creados no se traducen en vivo; la
   ventana lo avisa.
2. **Esquema 2 de `settings.json`** con la sección `user` (tema, Mica, densidad, modo de ocultación, intervalo de
   comprobación, idioma, inicio con Windows, introducción vista). Las migraciones se aplican sobre el JSON, en cadena
   v(n) → v(n+1), antes de deserializar; el archivo se reescribe migrado. Quien ya tenía un archivo v1 no ve la
   introducción. Una versión desconocida o más nueva se aparta como `.corrupt-<fecha>`, como cualquier archivo
   corrupto. Los valores fuera de rango se corrigen (`UserSettings.Normalize`) en vez de rechazar el archivo.
3. **Argumentos de línea de comandos:** `--lang`, `--theme` y `--no-mica` sustituyen a `settings.json` solo en esa
   ejecución. Nuevos: `--settings`, `--onboarding` y `--export-diagnostics <ruta.zip>`. Este último no muestra
   interfaz, no agrupa, no escribe logs y funciona con otra instancia en marcha.
4. **Inicio con Windows:** valor `TabDock` en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, con la ruta entre
   comillas. Sin administrador y solo para el usuario actual. El registro manda: al arrancar, la casilla se sincroniza
   con él. Una entrada que apunta a otra copia del ejecutable cuenta como desactivada, y al activarla se corrige.
5. **Introducción de 3 pasos en el primer arranque:** elegir programas → ajuste recomendado de VS Code (texto para
   copiar; TabDock nunca modifica la configuración de VS Code) → probar `Ctrl+Alt+M`. El paso 3 confirma el agrupado
   cuando se crea un grupo. Cerrarla o saltarla la da por vista; se puede repetir desde Ajustes. Sustituye al aviso de
   la bandeja de la Fase 4.
6. **Diagnóstico anonimizado:** ZIP elegido por el usuario con `LEEME.txt`, `system.json` (versiones, arquitectura,
   idioma, monitores y escala), `settings.json` (nombres de regla → `regla-N`, patrones de ruta → hash) y los 5 logs más
   recientes. Los logs ya están enmascarados; se les aplica una segunda pasada que sustituye la carpeta de perfil
   (también escapada en JSON), el usuario, el equipo y el dominio por marcadores. No incluye `preferences.json` (alias,
   nombres de proyecto) ni `state.json`. No se envía a ningún sitio.
7. **Atajos editables:** al enfocar un atajo, los globales se liberan (`IHotkeyControl.SuspendAll`), porque un atajo
   registrado nunca llega a la ventana con el foco. Al salir, se vuelven a registrar. Validación previa
   (`HotkeyValidator`): debe incluir Ctrl, Alt o Win, no puede ser solo un modificador, `Ctrl+Alt+Tab` está reservado y
   no puede repetir el de otro comando activo. Los conflictos con otros programas se marcan en su fila tras registrar.

## Alternativas descartadas

- **Quitar la barra de título de programas ajenos** (opción experimental del prompt): contradice el modelo de la
  ADR 0004, en el que esa barra hace de barra del grupo, y exige cambiar estilos de ventanas ajenas. No se ofrece;
  Ajustes → Avanzado lo explica.
- **Tarea programada** para el inicio con Windows: necesita más permisos y no aporta nada a una aplicación de usuario.
- **Leer `StartupApproved\Run`** (lo que desactiva el Administrador de tareas): formato no documentado.

## Consecuencias

- `HideMode.TaskbarOnly` existe en el modelo y aparece desactivado en Ajustes; un archivo que lo pida usa `Hide`.
- La posición de la barra lateral (`SidebarPosition.Right`) sigue sin implementarse en el marco; no se muestra en
  Ajustes (Fase 8).
- Un error al crear una ventana (p. ej. XAML) sigue terminando TabDock tras la restauración de emergencia: las ventanas
  vuelven a su sitio, pero la aplicación se cierra.
