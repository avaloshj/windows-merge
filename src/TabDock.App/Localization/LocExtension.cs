using System.Windows.Markup;

namespace TabDock.App.Localization;

/// <summary>Uso en XAML: <c>Text="{loc:Loc Diag_Title}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => TextResources.Get(Key);
}
