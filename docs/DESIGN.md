# Sistema visual y de movimiento

Referencia de los tokens de `src/TabDock.App/Themes/`. Las vistas nunca usan colores, tamaños ni duraciones literales.

## Archivos

| Archivo | Contenido | Cuándo cambia |
|---|---|---|
| `Tokens.xaml` | Tipografía, tamaños, radios | Nunca en ejecución |
| `Light.xaml` / `Dark.xaml` | `Color.*` y `Brush.*` (mismas claves) | Al cambiar el tema del sistema o la preferencia |
| `HighContrast.xaml` | `Brush.*` enlazados a `SystemColors` | Con el alto contraste de Windows |
| `Motion.xaml` / `Motion.Reduced.xaml` | Easing, duraciones, desplazamientos | Con "Reducir animaciones" de Windows |
| `Icons.xaml` | Geometrías Lucide 1.57.0 (ISC) | — |
| `Controls.xaml` | Estilos de controles; se carga el último | — |

`ThemeService` compone los diccionarios en ese orden antes de crear ninguna ventana. Los colores se usan siempre con
`DynamicResource`, para que el cambio de tema se aplique en caliente. Tokens y movimiento se usan con `StaticResource`.

## Desviaciones respecto al diseño original (justificadas)

| Token | Diseño | Implementado | Motivo |
|---|---|---|---|
| `Color.TextSecondary` (claro) | `#6B7280` | `#5F6672` | Sobre la pestaña con hover: 3.95:1 → 4.73:1 (WCAG AA ≥ 4.5) |
| `Color.TextSecondary` (oscuro) | `#9A9AA2` | `#A8A8B0` | Sobre la pestaña activa `#3A3A3F`: 4.05:1 → 4.79:1 |

`tests/TabDock.App.Tests/Themes/ContrastTests.cs` verifica todos los pares texto/fondo de ambos temas, con los fondos
translúcidos (hover, búsqueda) compuestos sobre la barra lateral.

## Limitaciones de la plataforma

- **Radio de la ventana:** en Windows 11 el redondeo lo aplica DWM con un radio fijo del sistema (≈8 px). Los 12 px del
  diseño solo serían posibles con una ventana en capas transparente, que se descartó por rendimiento y porque
  impide usar Mica (ADR 0001).
- **Tracking +0.06em** en los encabezados de sección: WPF no permite espaciado entre letras en `TextBlock`. Se usa
  mayúscula, 11 px y SemiBold sin tracking.
- **Mica:** con Mica activa la barra lateral es translúcida y su color depende del fondo de escritorio, así que su
  contraste no se puede garantizar. Si el usuario necesita contraste fijo puede desactivarla (`--no-mica`; opción en
  Apariencia en la Fase 7). En alto contraste Mica se desactiva siempre.
- **Inter** se empaqueta en tres pesos estáticos (Regular, Medium, SemiBold): WPF no interpreta los ejes de las
  fuentes variables.
- **Cambio de "Reducir animaciones" en caliente:** las plantillas ya creadas conservan sus duraciones; el cambio
  afecta a las ventanas que se abran después.

## Movimiento

| Interacción | Animación | Reducida |
|---|---|---|
| Apertura del marco | Fundido + escala 0.98 → 1, 250 ms | Fundido 120 ms |
| Entrada de pestañas | Fundido + X −8 → 0 px, 300 ms, escalonado 40 ms (máx. 10) | Fundido 120 ms, sin escalonado |
| Cambio de pestaña | Indicador se desliza 200 ms; la ventana gestionada cambia al instante | Indicador sin animación |
| Cierre de pestaña | Colapso de altura + fundido, 180 ms; se elimina al terminar | Fundido 120 ms |
| Hover | Fondo 120 ms | Igual |
| Pulsación | Escala 0.98 | Sin escala |

Easing principal `cubic-bezier(0.22, 1, 0.36, 1)` y salida `cubic-bezier(0.4, 0, 1, 1)`, implementados con
`CubicBezierEase` (pruebas de monotonía y ausencia de overshoot en `CubicBezierTests`).

Componentes envoltorio (`Controls/Motion/`): `Entrance`, `PressScale` y `CollapseOnClose` (propiedades adjuntas),
`Animator.RunAsync` (animación que devuelve `Task`) y `SidebarTabList` (indicador deslizante).

## Botones de ventana

Estilo Windows 11 (ADR 0004): 46 × 40 px, glifos Segoe Fluent Icons / Segoe MDL2 Assets, hover `Brush.CaptionHover`,
cerrar `#C42B1C` con glifo blanco. Con la ventana inactiva los glifos usan `Brush.TextSecondary`.
