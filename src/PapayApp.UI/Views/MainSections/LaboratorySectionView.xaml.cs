namespace PapayApp.UI.Views.MainSections;
public partial class LaboratorySectionView : ContentView
{
    public LaboratorySectionView() { InitializeComponent(); }

    private bool? _isCompactLayout;

    private void OnWorkspaceSizeChanged(object? sender, EventArgs e)
    {
        if (GeneratorWorkspace.Width <= 0)
            return;

        var compact = GeneratorWorkspace.Width < 820;
        if (_isCompactLayout == compact)
            return;

        _isCompactLayout = compact;
        GeneratorWorkspace.ColumnDefinitions.Clear();
        GeneratorWorkspace.RowDefinitions.Clear();
        GeneratorWorkspace.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = compact ? GridLength.Star : new GridLength(340)
        });
        GeneratorWorkspace.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = compact ? new GridLength(0) : GridLength.Star
        });
        GeneratorWorkspace.RowDefinitions.Add(new RowDefinition
        {
            Height = compact ? GridLength.Auto : GridLength.Star
        });
        GeneratorWorkspace.RowDefinitions.Add(new RowDefinition
        {
            Height = compact ? GridLength.Star : new GridLength(0)
        });
        ConfigurationScroll.HeightRequest = compact ? 260 : -1;
        Grid.SetColumn(PatientsPanel, compact ? 0 : 1);
        Grid.SetRow(PatientsPanel, compact ? 1 : 0);
    }
}
