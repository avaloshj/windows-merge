using CommunityToolkit.Mvvm.ComponentModel;
using TabDock.App.Localization;
using TabDock.Core.Domain;

namespace TabDock.App.ViewModels;

/// <summary>Una pestaña de la barra lateral (una ventana gestionada).</summary>
public sealed partial class TabViewModel : ObservableObject
{
    public TabViewModel(string displayName, string colorHex)
        : this(Guid.NewGuid(), displayName, colorHex)
    {
    }

    /// <summary>Pestaña de una ventana gestionada: comparte el identificador de <c>ManagedWindow</c>.</summary>
    public TabViewModel(Guid id, string displayName, string colorHex)
    {
        Id = id;
        DisplayName = displayName;
        BaseName = displayName;
        ColorHex = colorHex;
        Subtitle = string.Empty;
        RenameText = string.Empty;
    }

    /// <summary>El usuario confirmó el nombre escrito en <see cref="RenameText"/>.</summary>
    public event EventHandler? RenameCommitted;

    public Guid Id { get; }

    /// <summary>Nombre de proyecto detectado en el título (sin alias): clave de las preferencias guardadas.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Nombre calculado sin alias (proyecto o "Ventana vacía N").</summary>
    public string BaseName { get; set; }

    /// <summary>Nombre elegido por el usuario; tiene prioridad sobre <see cref="BaseName"/>.</summary>
    public string? Alias { get; set; }

    /// <summary>Color elegido por el usuario; si falta, el determinista por nombre de proyecto.</summary>
    public string? ColorOverride { get; set; }

    /// <summary>Posición guardada de la última sesión; solo se usa al insertar la pestaña.</summary>
    public int? SavedOrder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial string Subtitle { get; set; }

    [ObservableProperty]
    public partial string ColorHex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    public partial RemoteKind RemoteKind { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    public partial bool IsUnresponsive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    public partial bool IsPinned { get; set; }

    /// <summary>La pestaña se está cerrando: la vista anima su salida y después pide eliminarla.</summary>
    [ObservableProperty]
    public partial bool IsClosing { get; set; }

    [ObservableProperty]
    public partial bool IsRenaming { get; set; }

    [ObservableProperty]
    public partial string RenameText { get; set; }

    /// <summary>Nombre para lectores de pantalla: incluye los estados que la vista solo comunica con color o iconos.</summary>
    public string AutomationName
    {
        get
        {
            var parts = new List<string>(5) { DisplayName };
            if (IsPinned)
                parts.Add(TextResources.Get("Tab_Pinned"));

            if (IsDirty)
                parts.Add(TextResources.Get("Tab_Dirty"));

            if (RemoteKind != RemoteKind.None)
                parts.Add(TextResources.Get("Remote_" + RemoteKind.ToString()));

            if (IsUnresponsive)
                parts.Add(TextResources.Get("Tab_Unresponsive"));

            return string.Join(", ", parts);
        }
    }

    public void BeginRename()
    {
        RenameText = Alias ?? DisplayName;
        IsRenaming = true;
    }

    public void CommitRename()
    {
        if (!IsRenaming)
            return;

        IsRenaming = false;
        RenameCommitted?.Invoke(this, EventArgs.Empty);
    }

    public void CancelRename() => IsRenaming = false;
}
