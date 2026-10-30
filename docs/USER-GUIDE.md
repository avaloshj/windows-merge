# Guía de uso de TabDock

TabDock reúne todas las ventanas de un programa (por defecto, Visual Studio Code) en un solo marco con pestañas
verticales. Las ventanas siguen siendo las de siempre: TabDock solo las coloca, las oculta y las muestra.

## Primeros pasos

1. Abre TabDock. Se queda en la bandeja del sistema (icono azul con una «T»).
2. La primera vez aparece una introducción de 3 pasos:
   1. Elige qué programas quieres agrupar.
   2. Ajuste recomendado de VS Code (opcional, se copia con un botón).
   3. Prueba el atajo.
3. Abre dos o más ventanas de VS Code y pulsa **Ctrl+Alt+M**: se agrupan en un marco.

### Ajuste recomendado para VS Code (opcional)

En VS Code, pulsa Ctrl+Mayús+P, elige «Preferences: Open User Settings (JSON)» y añade:

```json
"window.titleBarStyle": "custom",
"window.title": "${rootName}${separator}${activeEditorShort}${separator}${appName}"
```

Con él, la barra de título de VS Code hace de barra del grupo y TabDock reconoce mejor el nombre de cada proyecto.
TabDock nunca cambia la configuración de VS Code por su cuenta.

## Uso diario

- **Cambiar de ventana:** clic en su pestaña, o **Ctrl+Alt+AvPág** / **Ctrl+Alt+RePág**, o **Ctrl+Alt+1…9**.
- **Buscar una ventana:** **Ctrl+Alt+P** y escribe parte del nombre del proyecto o del archivo.
- **Barra lateral:** **Ctrl+Alt+B** la pliega o despliega; arrastra su borde para cambiar el ancho.
- **Ventanas nuevas:** con un grupo abierto, una ventana nueva del mismo programa se añade sola.
- **Maximizar, minimizar y cerrar:**
  - Con VS Code con barra personalizada, los botones de la propia ventana actúan sobre el grupo entero; su botón de
    cerrar cierra solo esa ventana (esa pestaña).
  - Con programas de barra de título normal de Windows, esa barra queda oculta y los botones son los de la barra del
    marco; cerrar el marco separa todas las ventanas.
- **Snap Layouts:** coloca la ventana activa en una zona de la pantalla y el grupo entero la ocupa.
- **Separar todas:** **Ctrl+Alt+U** devuelve cada ventana a donde estaba.

### Menú de una pestaña (clic derecho)

Renombrar (también doble clic o F2), cambiar color, fijar arriba, separar del grupo, mover a otro grupo, cerrar y
cerrar las demás. Arrastra una pestaña para reordenarla, o fuera de la barra para separarla. Alias, colores, fijadas y
orden se recuerdan por proyecto.

## Configuración

Bandeja → **Configuración…**:

- **General:** idioma (español o inglés, al reiniciar), iniciar con Windows y volver a ver la introducción.
- **Programas:** activa o desactiva programas; elige si las ventanas nuevas se agrupan solas y si se agrupan al
  iniciar TabDock; añade cualquier programa que tenga ventanas abiertas.
- **Apariencia:** tema (igual que Windows, claro u oscuro), fondo Mica y densidad de la barra lateral.
- **Atajos:** haz clic en un atajo y pulsa la nueva combinación (debe incluir Ctrl, Alt o Win; Esc cancela). Si otro
  programa ya lo usa, se avisa en la fila.
- **Avanzado:** intervalo de la comprobación periódica y **modo de bajo consumo de memoria** (dibuja sin aceleración
  gráfica: unos 100 MB menos de memoria, sin Mica; se aplica al reiniciar).
- **Diagnóstico:** exportar un archivo ZIP para informar de un problema, abrir la carpeta de registros y ver las
  ventanas detectadas.

## Privacidad

- TabDock no se conecta a internet ni envía datos.
- Los registros no guardan títulos de ventana, rutas ni nombres de proyecto: solo un código y la longitud.
- El diagnóstico exportado no incluye alias ni nombres de proyecto, ni tu nombre de usuario o de equipo. Se guarda
  donde tú elijas y no se envía a ningún sitio.
- TabDock nunca lee el contenido de otras ventanas. Las vistas previas de las pestañas las compone Windows en vivo y
  no se guardan.

## Si algo va mal

- **Salir** (bandeja → Salir) devuelve todas las ventanas a su sitio.
- Si TabDock se cierra de golpe, un proceso guardián devuelve las ventanas; si eso también falla, lo hace el siguiente
  arranque de TabDock. Desinstalarlo también las devuelve.
- Una ventana que no responde muestra «no responde» en su pestaña; TabDock sigue funcionando.
- Las ventanas de programas ejecutados como administrador no se pueden agrupar (Windows lo impide); TabDock avisa.

## Limitaciones conocidas

- Alt+Tab muestra el marco y la ventana activa por separado.
- Las pestañas ocultas no cambian de escritorio virtual; el marco sigue a la ventana activa.
- Con ventanas agrupadas, el marco se ve con esquinas rectas y sin Mica (tapa la sombra de la ventana activa).
- La barra lateral a la derecha todavía no está disponible.

## Línea de comandos

| Opción | Efecto |
|---|---|
| `--settings` | Abre la configuración |
| `--onboarding` | Muestra la introducción |
| `--export-diagnostics <ruta.zip>` | Exporta el diagnóstico sin abrir ventanas |
| `--lang es\|en`, `--theme system\|light\|dark`, `--no-mica` | Sustituyen la configuración solo en esa ejecución |
| `--diagnostics` | Abre la ventana de ventanas detectadas |
| `--verbose` | Registros detallados |
