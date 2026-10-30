using System.Globalization;
using System.Resources;

namespace TabDock.App.Localization;

/// <summary>Acceso a los textos localizados de <c>Strings.resx</c> (español) y <c>Strings.en.resx</c>.</summary>
internal static class TextResources
{
    public const string BaseName = "TabDock.App.Localization.Strings";

    public static ResourceManager Manager { get; } = new(BaseName, typeof(TextResources).Assembly);

    /// <summary>Devuelve el texto; si falta la clave muestra <c>[clave]</c> para que se detecte en pruebas manuales.</summary>
    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
