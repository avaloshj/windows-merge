namespace TabDock.Core.Domain;

/// <summary>Modificadores de un atajo global (mismos valores que <c>MOD_*</c> de <c>RegisterHotKey</c>).</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}
