using System.Collections.ObjectModel;
using System.Globalization;
using PapayApp.UI.Services;

namespace PapayApp.UI.Controls;

public partial class HelpCenterView : ContentView
{
    private bool _ready;
    public ObservableCollection<HelpArticle> Articles { get; } = [];

    public HelpCenterView()
    {
        InitializeComponent();
        BindingContext = this;
        VersionLabel.Text = $"Versión instalada: {AppInfo.Current.VersionString}";
        SectionPicker.ItemsSource = new[] { "Todos los apartados" }.Concat(HelpCatalog.Articles.Select(a => a.Section).Distinct()).ToList();
        LevelPicker.ItemsSource = new[] { "Todo el contenido", HelpCatalog.Guide, HelpCatalog.Technical, HelpCatalog.Problems };
        SectionPicker.SelectedIndex = LevelPicker.SelectedIndex = 0;
        _ready = true;
        Filter();
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        if (_ready) Filter();
    }

    private void Filter()
    {
        var query = SearchInput.Text?.Trim() ?? "";
        var found = HelpCatalog.Articles.Where(a =>
            (SectionPicker.SelectedIndex <= 0 || a.Section == (string?)SectionPicker.SelectedItem) &&
            (LevelPicker.SelectedIndex <= 0 || a.Level == (string?)LevelPicker.SelectedItem) &&
            (query.Length == 0 || CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                $"{a.Category} {a.Title} {a.Body}", query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)).ToList();
        Articles.Clear();
        foreach (var article in found) Articles.Add(article);
        EmptyMessage.IsVisible = found.Count == 0;
        ResultsList.IsVisible = found.Count > 0;
        ResultsLabel.Text = $"{found.Count} de {HelpCatalog.Articles.Count} temas. La búsqueda incluye títulos y explicaciones.";
    }
}
