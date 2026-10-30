namespace TabDock.App.ViewModels;

/// <summary>Una pestaña de cualquier grupo en la búsqueda rápida.</summary>
public sealed record QuickSearchItem(Guid TabId, Guid GroupId, string Name, string Subtitle, string GroupTitle, string ColorHex)
{
    public string AutomationName => string.IsNullOrEmpty(Subtitle) ? $"{Name}, {GroupTitle}" : $"{Name}, {Subtitle}, {GroupTitle}";
}
