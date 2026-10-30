using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabDock.Core.Abstractions;

namespace TabDock.App.ViewModels;

/// <summary>Estado del marco anfitrión: pestañas, selección, búsqueda, barra lateral y acciones sobre pestañas.</summary>
public sealed partial class HostFrameViewModel : ObservableObject
{
    public const double DefaultSidebarWidth = 240;
    public const double MinSidebarWidth = 180;
    public const double MaxSidebarWidth = 360;
    public const double CollapsedSidebarWidth = 56;

    public HostFrameViewModel()
    {
        // La vista debe existir antes de asignar SearchText: su cambio refresca el filtro.
        TabsView = CollectionViewSource.GetDefaultView(Tabs);
        TabsView.Filter = MatchesSearch;
        Tabs.CollectionChanged += OnTabsChanged;

        Title = string.Empty;
        SearchText = string.Empty;
        SidebarWidth = DefaultSidebarWidth;
        UpdateEmptyState();
    }

    [RelayCommand]
    public void ReactivateSelected() => SelectedTabReactivated?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// El usuario pidió cerrar una pestaña (botón × o menú). Con suscriptores, la pestaña no se quita aquí: se quita
    /// cuando la ventana se cierra de verdad (puede preguntar si guardar). Sin suscriptores (demostración), se cierra.
    /// </summary>
    public event EventHandler<TabEventArgs>? TabCloseRequested;

    /// <summary>Renombrar, color, fijar, reordenar, desacoplar o mover: lo atiende el coordinador.</summary>
    public event EventHandler<TabActionEventArgs>? TabActionRequested;

    /// <summary>Clic en la pestaña ya activa: el coordinador vuelve a mostrar su ventana encima y le da el foco.</summary>
    public event EventHandler? SelectedTabReactivated;

    public ObservableCollection<TabViewModel> Tabs { get; } = [];

    /// <summary>Vista filtrada por <see cref="SearchText"/> que muestra la barra lateral.</summary>
    public ICollectionView TabsView { get; }

    /// <summary>Otros grupos abiertos (destinos de "Mover a otro grupo").</summary>
    public ObservableCollection<GroupReference> OtherGroups { get; } = [];

    /// <summary>Ventana de una pestaña (para la vista previa en vivo); la proporciona el coordinador.</summary>
    public Func<TabViewModel, WindowHandle?>? WindowResolver { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial TabViewModel? SelectedTab { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial double SidebarWidth { get; set; }

    [ObservableProperty]
    public partial bool IsSidebarCollapsed { get; set; }

    /// <summary>No hay ninguna ventana en el grupo.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>Hay ventanas, pero ninguna coincide con la búsqueda.</summary>
    [ObservableProperty]
    public partial bool HasNoResults { get; set; }

    /// <summary>Reordenar arrastrando solo tiene sentido sin filtro de búsqueda.</summary>
    public bool CanReorder => string.IsNullOrWhiteSpace(SearchText);

    public void Load(string title, IEnumerable<TabViewModel> tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        Title = title;
        Tabs.Clear();
        foreach (var tab in tabs)
            Tabs.Add(tab);

        SelectedTab = Tabs.FirstOrDefault();
    }

    /// <summary>
    /// Inserta respetando el orden de la barra: primero las fijadas; dentro de cada parte, por posición guardada;
    /// las que no tienen posición, al final de su parte.
    /// </summary>
    public void InsertTab(TabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        var key = SortKey(tab);
        var index = 0;
        while (index < Tabs.Count && SortKey(Tabs[index]).CompareTo(key) <= 0)
            index++;

        Tabs.Insert(index, tab);
    }

    /// <summary>Mueve una pestaña sin salir de su parte (fijadas arriba). Devuelve si cambió algo.</summary>
    public bool MoveTab(TabViewModel tab, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(tab);

        var from = Tabs.IndexOf(tab);
        if (from < 0)
            return false;

        var pinnedCount = Tabs.Count(t => t.IsPinned);
        var (min, max) = tab.IsPinned ? (0, pinnedCount - 1) : (pinnedCount, Tabs.Count - 1);
        var to = Math.Clamp(targetIndex, min, max);
        if (to == from)
            return false;

        Tabs.Move(from, to);
        return true;
    }

    /// <summary>Fin de un arrastre: se guarda el orden.</summary>
    public void CompleteReorder(TabViewModel tab) => Raise(tab, TabAction.Reorder);

    /// <summary>Empieza a renombrar la pestaña (solo una a la vez).</summary>
    public void Rename(TabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        foreach (var other in Tabs.Where(t => t.IsRenaming && !ReferenceEquals(t, tab)))
            other.CancelRename();

        tab.BeginRename();
    }

    public void SetColor(TabViewModel tab, string? colorHex) => Raise(tab, TabAction.ChangeColor, colorHex);

    /// <summary>Fija o desfija y recoloca la pestaña al final de la parte que le corresponde.</summary>
    public void TogglePin(TabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        tab.IsPinned = !tab.IsPinned;
        var from = Tabs.IndexOf(tab);
        var to = tab.IsPinned ? Tabs.Count(t => t.IsPinned) - 1 : Tabs.Count(t => t.IsPinned);
        if (from >= 0 && from != to)
            Tabs.Move(from, Math.Clamp(to, 0, Tabs.Count - 1));

        Raise(tab, TabAction.TogglePin);
        Raise(tab, TabAction.Reorder);
    }

    public void Detach(TabViewModel tab, (int X, int Y)? dropPoint = null) => Raise(tab, TabAction.Detach, dropPoint);

    public void MoveToGroup(TabViewModel tab, Guid groupId) => Raise(tab, TabAction.MoveToGroup, groupId);

    public void CloseOthers(TabViewModel tab)
    {
        foreach (var other in Tabs.Where(t => !ReferenceEquals(t, tab) && !t.IsClosing).ToList())
            CloseTab(other);
    }

    partial void OnSearchTextChanged(string value)
    {
        TabsView.Refresh();
        UpdateEmptyState();
        OnPropertyChanged(nameof(CanReorder));
    }

    partial void OnSidebarWidthChanged(double value)
    {
        var clamped = Math.Clamp(value, MinSidebarWidth, MaxSidebarWidth);
        if (!clamped.Equals(value))
            SidebarWidth = clamped;
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    /// <summary>Marca la pestaña para cerrarse; la vista anima la salida y llama a <see cref="RemoveTabCommand"/>.</summary>
    public void BeginRemoveTab(TabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (tab.IsClosing)
            return;

        if (ReferenceEquals(SelectedTab, tab))
            SelectedTab = NeighbourOf(tab);

        tab.IsClosing = true;
    }

    [RelayCommand]
    private void CloseTab(TabViewModel? tab)
    {
        if (tab is null || tab.IsClosing)
            return;

        if (TabCloseRequested is { } handler)
            handler(this, new TabEventArgs(tab));
        else
            BeginRemoveTab(tab);
    }

    [RelayCommand]
    private void RemoveTab(TabViewModel? tab)
    {
        if (tab is not null)
            Tabs.Remove(tab);
    }

    /// <summary>Selecciona la pestaña N (1–9) entre las visibles.</summary>
    [RelayCommand]
    private void SelectTabAt(string? position)
    {
        if (!int.TryParse(position, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
            return;

        var visible = TabsView.Cast<TabViewModel>().Where(t => !t.IsClosing).ToList();
        if (number <= visible.Count)
            SelectedTab = visible[number - 1];
    }

    /// <summary>Pestaña siguiente (+1) o anterior (−1) en orden visual, de forma circular.</summary>
    public void SelectRelative(int delta)
    {
        var visible = TabsView.Cast<TabViewModel>().Where(t => !t.IsClosing).ToList();
        if (visible.Count == 0)
            return;

        var current = SelectedTab is null ? -1 : visible.IndexOf(SelectedTab);
        var next = current < 0 ? 0 : ((current + delta) % visible.Count + visible.Count) % visible.Count;
        SelectedTab = visible[next];
    }

    private static (int Section, int Order) SortKey(TabViewModel tab) => (tab.IsPinned ? 0 : 1, tab.SavedOrder ?? int.MaxValue);

    private void Raise(TabViewModel tab, TabAction action, object? value = null)
    {
        ArgumentNullException.ThrowIfNull(tab);
        TabActionRequested?.Invoke(this, new TabActionEventArgs(tab, action, value));
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var tab in e.NewItems?.OfType<TabViewModel>() ?? [])
            tab.RenameCommitted += OnRenameCommitted;

        foreach (var tab in e.OldItems?.OfType<TabViewModel>() ?? [])
            tab.RenameCommitted -= OnRenameCommitted;

        UpdateEmptyState();
    }

    private void OnRenameCommitted(object? sender, EventArgs e)
    {
        if (sender is TabViewModel tab)
            Raise(tab, TabAction.Rename, string.IsNullOrWhiteSpace(tab.RenameText) ? null : tab.RenameText.Trim());
    }

    private bool MatchesSearch(object item) =>
        string.IsNullOrWhiteSpace(SearchText)
        || (item is TabViewModel tab
            && (tab.DisplayName.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase)
                || tab.Subtitle.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase)));

    /// <summary>La siguiente pestaña en orden visual o, si era la última, la anterior.</summary>
    private TabViewModel? NeighbourOf(TabViewModel tab)
    {
        var alive = Tabs.Where(t => !t.IsClosing).ToList();
        var index = alive.IndexOf(tab);
        if (alive.Count <= 1 || index < 0)
            return null;

        return index + 1 < alive.Count ? alive[index + 1] : alive[index - 1];
    }

    private void UpdateEmptyState()
    {
        IsEmpty = Tabs.Count == 0;
        HasNoResults = !IsEmpty && !TabsView.Cast<object>().Any();
    }
}
