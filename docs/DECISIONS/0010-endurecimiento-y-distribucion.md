# ADR 0010 — Endurecimiento, rendimiento y distribución

- **Estado:** Aceptado (2026-10-30)
- **Ámbito:** `TabGroupSession`, `JsonSettingsStore`, `WindowLauncher`, `UserSettings.LowMemoryRendering`,
  `Installer`, `build/publish.ps1`, `build/e2e-*.ps1`

## Decisiones

1. **Eventos de ventana atrasados.** Los eventos de Windows llegan con retraso y a veces son ecos de lo que hizo el
   propio TabDock. Antes de actuar sobre un evento se comprueba el estado **actual**:
   - `Shown` de una pestaña oculta: solo la activa si sigue visible. El eco de un "mostrar" de TabDock, recibido
     después de volver a ocultarla, reactivaba pestañas antiguas en cadena: 2941 activaciones en un minuto con 20
     ventanas y cambios rápidos.
   - `Foreground` de una pestaña oculta: solo si sigue en primer plano.
   - `Destroyed`: el aviso llega al **empezar** a destruirse la ventana, y Chromium lo emite también al ocultarla sin
     destruirla. Si la ventana aún existe, se vuelve a comprobar a los 250 ms (antes de que Windows reutilice su HWND).
2. **Valores por defecto en `settings.json`.** La deserialización generada pone `false`/0/`null` en las propiedades
   `init` ausentes. Antes de deserializar, las secciones `user`, cada regla y cada atajo se completan con los valores
   por defecto del propio modelo (una regla editada a mano sin `enabled` ya no queda desactivada).
3. **Ventanas que fallan al abrirse** (Ajustes, introducción, diagnóstico): `WindowLauncher` registra el error y avisa
   en la bandeja; TabDock sigue funcionando.
4. **Memoria.** Medido con 1, 20 y 50 ventanas: las pestañas no cuestan memoria apreciable; el coste es fijo. En
   reposo, unos 140 MB de conjunto de trabajo y 85 MB privados. Al crear el marco, unos 110 MB más, sobre todo por el
   dispositivo gráfico de WPF y los búferes de una ventana de 2560×1440. Ni ajustar el recolector de memoria ni
   compilar en Release cambian las cifras. Se ofrece un **modo de bajo consumo** (Ajustes → Avanzado, al reiniciar):
   render por software y sin Mica. Con él, unos 85–88 MB privados con 20 ventanas. Por defecto se mantiene la
   aceleración gráfica (calidad visual, Mica).
5. **Distribución:** `dotnet publish` autocontenido y ReadyToRun para x64 y arm64 (unos 140 y 150 MB). `PublishTrimmed`
   desactivado: WPF no admite recorte.
6. **Velopack 1.2.0** solo para los avisos del instalador. Al desinstalar restaura las ventanas pendientes y quita el
   inicio con Windows. **No se buscan actualizaciones** (política sin red): un comprobador desactivable y solo HTTPS se
   añadiría con `UpdateManager` y un feed configurado.
7. **Firma Authenticode** en `publish.ps1 -Sign`, con certificado, contraseña y servidor de sellado de tiempo tomados de
   variables de entorno que define quien firma. Ningún certificado en el repositorio.
8. **Scripts de prueba seguros** (`build/e2e-common.ps1`, tras perderse un `settings.json` real durante una medición):
   - Un TabDock que no lanzó la prueba no se cierra.
   - Una copia `.e2e-backup` que ya existe es de una prueba interrumpida: se restaura antes de empezar y nunca se
     sobrescribe.
   - `state.json` con ventanas que siguen existiendo no se borra.
   - La salida de los scripts no se corta con `Select-Object -First`: PowerShell los detiene sin ejecutar su limpieza.

## Consecuencias

- Objetivos de la especificación, medidos con `build/e2e-stress.ps1` (compilación Debug):
  - **Arranque:** 0,55–1,3 s, dentro del objetivo.
  - **CPU en reposo:** 0–0,2 % con 20 ventanas, con picos ocasionales del 1–1,4 % según la carga del equipo.
  - **Cambio de pestaña:** media de 20–35 ms. El máximo queda normalmente por debajo de 50 ms, con picos ocasionales
    de 60–75 ms (sobre todo en modo de bajo consumo).
  - **Memoria:** < 120 MB privados solo en modo de bajo consumo.
- 50 ventanas y una ráfaga de 100 aperturas y cierres: sin pestañas fantasma ni ventanas ocultas tras separar.
- El paquete de Velopack y la firma necesitan `vpk` y un certificado; no se han
  ejecutado.
- Sin integración continua alojada (se descartó GitHub Actions a petición del usuario): la verificación se hace en
  local con `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` y los scripts `build/e2e-*.ps1`.
