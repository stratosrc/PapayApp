using PapayApp.Core.Models;
using PapayApp.Core.Services;
using PapayApp.Infrastructure.Services;
using PapayApp.UI.Services;

namespace PapayApp.UI.Controls;
public partial class LaboratoryTemplatesEditor : ContentView
{
    private ILaboratoryTemplateStore? _store;
    private IFileSelectionService? _files;
    private LaboratoryTemplateSettings? _draft;
    private bool _busy;
    public LaboratoryTemplatesEditor() { InitializeComponent(); }
    public void Configure(ILaboratoryTemplateStore store, IFileSelectionService files)
    {
        _store = store; _files = files;
        try { _draft = store.Load(); Refresh(); }
        catch (Exception e) { StatusLabel.Text = UserProblem.Describe(e, "cargar las plantillas de laboratorio"); }
    }
    private void Refresh()
    {
        ToxicologyLabel.Text = _draft?.ToxicologyName ?? "No disponible";
        SerologyLabel.Text = _draft?.SerologyName ?? "No disponible";
    }
    private async Task PickAsync(bool toxicology)
    {
        if (_busy || _files is null || _store is null) return;
        _busy = true; IsEnabled = false;
        try
        {
            var file = await _files.PickSingleFileAsync("Selecciona una plantilla Word con marcadores", [".docx"]);
            if (file is null) return;
            LaboratoryDocxTemplate.Validate(file.FullPath);
            var cachedPath = Path.Combine(FileSystem.CacheDirectory, $"lab-draft-{Guid.NewGuid():N}.docx");
            File.Copy(file.FullPath, cachedPath);
            _draft ??= _store.Defaults;
            if (toxicology) { _draft.ToxicologyPath = cachedPath; _draft.ToxicologyName = file.FileName; }
            else { _draft.SerologyPath = cachedPath; _draft.SerologyName = file.FileName; }
            Refresh();
            if (toxicology) ToxicologyLabel.Text = file.FileName; else SerologyLabel.Text = file.FileName;
            StatusLabel.Text = "Cambios sin guardar. Pulsa Guardar plantillas de laboratorio para aplicarlos.";
        }
        catch (Exception e) { StatusLabel.Text = UserProblem.Describe(e, "cambiar la plantilla"); }
        finally { _busy = false; IsEnabled = true; }
    }
    private async void OnPickToxicology(object? sender, EventArgs e) => await PickAsync(true);
    private async void OnPickSerology(object? sender, EventArgs e) => await PickAsync(false);
    private void OnSave(object? sender, EventArgs e)
    {
        if (_busy || _store is null || _draft is null) return;
        try { _store.Save(_draft); _draft = _store.Load(); Refresh(); StatusLabel.Text = "Plantillas de laboratorio guardadas."; }
        catch (Exception ex) { StatusLabel.Text = UserProblem.Describe(ex, "guardar las plantillas de laboratorio"); }
    }
    private void OnDiscard(object? sender, EventArgs e)
    {
        if (_busy || _store is null) return;
        try { _draft = _store.Load(); Refresh(); StatusLabel.Text = "Se recuperaron las plantillas guardadas."; }
        catch (Exception ex) { StatusLabel.Text = UserProblem.Describe(ex, "recuperar las plantillas de laboratorio"); }
    }
    private void OnRestore(object? sender, EventArgs e)
    {
        if (_busy || _store is null) return;
        _draft = _store.Defaults; Refresh(); StatusLabel.Text = "Originales preparados. Pulsa Guardar para activarlos.";
    }
}
