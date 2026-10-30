using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TabDock.Core.Services;

namespace TabDock.App.ViewModels;

/// <summary>Búsqueda rápida (Ctrl+Alt+P): filtrado difuso por nombre de proyecto y archivo en todos los grupos.</summary>
public sealed partial class QuickSearchViewModel : ObservableObject
{
    private readonly IReadOnlyList<QuickSearchItem> _all;

    public QuickSearchViewModel(IReadOnlyList<QuickSearchItem> items)
    {
        _all = items ?? throw new ArgumentNullException(nameof(items));
        Query = string.Empty;
        Filter();
    }

    public ObservableCollection<QuickSearchItem> Results { get; } = [];

    [ObservableProperty]
    public partial string Query { get; set; }

    [ObservableProperty]
    public partial QuickSearchItem? Selected { get; set; }

    [ObservableProperty]
    public partial bool HasNoResults { get; set; }

    /// <summary>Mueve la selección (flechas arriba/abajo), de forma circular.</summary>
    public void MoveSelection(int delta)
    {
        if (Results.Count == 0)
            return;

        var index = Selected is null ? -1 : Results.IndexOf(Selected);
        Selected = Results[((index + delta) % Results.Count + Results.Count) % Results.Count];
    }

    partial void OnQueryChanged(string value) => Filter();

    /// <summary>Puntúa nombre, archivo y la combinación de ambos; gana la mejor.</summary>
    private void Filter()
    {
        var ranked = _all
            .Select(item => (Item: item, Score: Best(item)))
            .Where(r => r.Score is not null)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(r => r.Item)
            .ToList();

        Results.Clear();
        foreach (var item in ranked)
            Results.Add(item);

        Selected = Results.FirstOrDefault();
        HasNoResults = Results.Count == 0;
    }

    private int? Best(QuickSearchItem item)
    {
        int?[] scores =
        [
            FuzzyMatcher.Score(Query, item.Name),
            FuzzyMatcher.Score(Query, item.Subtitle),
            FuzzyMatcher.Score(Query, item.Name + " " + item.Subtitle),
        ];
        return scores.Max();
    }
}
