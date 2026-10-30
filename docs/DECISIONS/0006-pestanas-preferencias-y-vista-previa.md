# ADR 0006 — Funciones de pestaña, preferencias y vista previa

- **Estado:** Aceptado (2026-10-30)
- **Ámbito:** `TabPreferenceService`, `preferences.json`, `SidebarTabList`, `TabPreviewWindow`, `QuickSearchWindow`

## Decisión

1. **Preferencias por proyecto, no por ventana:** clave `(regla, nombre de proyecto normalizado)`; los HWND cambian
   entre sesiones. Alias, color, fijada y orden se guardan en `preferences.json` en el acto. Las ventanas sin carpeta
   ("Ventana vacía N") admiten alias y color solo durante la sesión. Purga en dos pasos: 90 días sin verse → se marca
   (`DeletedAt`) → se borra en el arranque siguiente; usar el proyecto antes lo rescata.
2. **Orden:** fijadas arriba; dentro de cada parte, la posición guardada; las nuevas, al final de su parte. Arrastrar
   reordena en vivo y guarda al soltar; no se puede cruzar de fijadas a no fijadas arrastrando (se usa "Fijar").
   Con un filtro de búsqueda activo no se puede reordenar.
3. **Desacoplar:** soltar una pestaña fuera de la barra (ni Esc ni ningún destino la acepta) devuelve la ventana a su
   estado original y la coloca bajo el cursor con su tamaño original.
4. **Mover a otro grupo:** se separa del origen y se captura en el destino como activa (snapshot nuevo).
5. **Vista previa (400 ms de hover):** `DwmRegisterThumbnail`, compuesta por DWM, nunca leída ni guardada (política de
   seguridad: no se lee el contenido de ventanas ajenas; por eso se descartó `PrintWindow`). Si la ventana no tiene
   superficie visible (las inactivas están ocultas con `SW_HIDE`), se muestra una tarjeta con nombre, archivo y color.
   La vista previa en vivo de pestañas inactivas requerirá el modo `taskbarOnly` (Fase 6/7).
6. **Búsqueda rápida (`Ctrl+Alt+P`):** paleta flotante sobre todas las pestañas de todos los grupos, coincidencia
   difusa (letras en orden, bonificación por inicio de palabra y letras seguidas, sin distinguir acentos).

## Consecuencias

- Pendiente de verificación visual (pantalla bloqueada durante las pruebas): qué compone DWM para una ventana oculta.
  `TestTarget --thumbnail-of <hwnd>` permite comprobarlo.
