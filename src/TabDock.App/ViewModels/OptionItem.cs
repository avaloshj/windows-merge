namespace TabDock.App.ViewModels;

/// <summary>Opción de una lista desplegable: valor, texto localizado y si se puede elegir.</summary>
public sealed record OptionItem<T>(T Value, string Label, bool IsEnabled = true)
{
    /// <summary>Narrador y UI Automation leen el nombre de cada elemento de la lista con <see cref="ToString"/>.</summary>
    public override string ToString() => Label;
}
