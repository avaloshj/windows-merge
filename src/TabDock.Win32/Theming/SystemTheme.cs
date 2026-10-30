using Microsoft.Win32;

namespace TabDock.Win32.Theming;

/// <summary>Preferencia de tema de aplicaciones del usuario (Configuración → Personalización → Colores).</summary>
public static class SystemTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    /// <summary>
    /// Lee <c>AppsUseLightTheme</c> (DWORD, 0 = oscuro). Es el valor que escribe la página de Configuración; no hay API
    /// Win32 pública equivalente (la alternativa es WinRT <c>UISettings</c>). Si falta, se asume tema claro.
    /// </summary>
    public static bool AppsUseLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(AppsUseLightThemeValue) is not int value || value != 0;
    }
}
