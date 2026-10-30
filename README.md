# TabDock

Utilidad residente para Windows 10/11 que agrupa todas las ventanas de un programa (por defecto, Visual Studio Code) en
un único marco con pestañas verticales: "Merge All Windows".

- Uso: [docs/USER-GUIDE.md](docs/USER-GUIDE.md)
- Arquitectura: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) y decisiones en [docs/DECISIONS](docs/DECISIONS/)
- Historial por fases: [docs/PHASES.md](docs/PHASES.md)
- Plan de pruebas: [docs/TEST-PLAN.md](docs/TEST-PLAN.md)

## Desarrollo

Requiere el SDK de .NET indicado en `global.json`.

```powershell
dotnet run --project src/TabDock.App                         # ejecutar
dotnet test --solution TabDock.sln                           # pruebas
dotnet format TabDock.sln --verify-no-changes                # formato
powershell -ExecutionPolicy Bypass -File build\e2e-<nombre>.ps1   # extremo a extremo (cierra antes TabDock)
powershell -ExecutionPolicy Bypass -File build\publish.ps1        # publicación x64/arm64
```

## Icono

`src/TabDock.App/Assets/TabDock.png` es el origen (idealmente de 256×256 con transparencia). Tras cambiarlo:

```powershell
powershell -ExecutionPolicy Bypass -File build\make-icon.ps1   # genera Assets\TabDock.ico (16–256 px)
```

El `.ico` lo usan el ejecutable (barra de tareas, Alt+Tab, Explorador, título de las ventanas), la bandeja del sistema
y el instalador de Velopack.

Sin red ni telemetría. Los registros enmascaran títulos, rutas y nombres de proyecto.
