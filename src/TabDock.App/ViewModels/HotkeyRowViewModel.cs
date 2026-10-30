using CommunityToolkit.Mvvm.ComponentModel;
using TabDock.App.Localization;
using TabDock.App.Services;
using TabDock.Core.Domain;

namespace TabDock.App.ViewModels;

/// <summary>Un comando en Ajustes → Atajos: combinación actual, si está activo y el último error o conflicto.</summary>
public sealed partial class HotkeyRowViewModel : ObservableObject
{
    public HotkeyRowViewModel(HotkeyBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        Command = binding.Command;
        CommandLabel = LabelFor(binding.Command);
        Binding = binding;
        KeysText = HotkeyText.Format(binding);
        IsActive = binding.Enabled;
    }

    public HotkeyCommand Command { get; }

    public string CommandLabel { get; }

    [ObservableProperty]
    public partial HotkeyBinding Binding { get; set; }

    [ObservableProperty]
    public partial string KeysText { get; set; }

    /// <summary>Error de validación o conflicto con otro programa; <see langword="null"/> si todo está bien.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; set; }

    [ObservableProperty]
    public partial bool IsCapturing { get; set; }

    /// <summary>Interruptor de la fila; <see cref="SettingsViewModel"/> guarda el cambio.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public bool HasMessage => Message is not null;

    /// <summary>Nombre localizado del comando, p. ej. "Ir a la pestaña 3".</summary>
    public static string LabelFor(HotkeyCommand command) => command switch
    {
        >= HotkeyCommand.GoToTab1 and <= HotkeyCommand.GoToTab9 => TextResources.Format("Command_GoToTab", command - HotkeyCommand.GoToTab1 + 1),
        _ => TextResources.Get("Command_" + command),
    };

    partial void OnBindingChanged(HotkeyBinding value)
    {
        KeysText = HotkeyText.Format(value);
        IsActive = value.Enabled;
    }
}
