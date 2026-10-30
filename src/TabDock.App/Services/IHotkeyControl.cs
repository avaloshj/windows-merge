using TabDock.Core.Domain;

namespace TabDock.App.Services;

/// <summary>Lo que la ventana de ajustes necesita de los atajos globales: conflictos y pausa durante la captura.</summary>
public interface IHotkeyControl
{
    IReadOnlyList<HotkeyBinding> Conflicts { get; }

    void SuspendAll();

    void ResumeAll();
}
