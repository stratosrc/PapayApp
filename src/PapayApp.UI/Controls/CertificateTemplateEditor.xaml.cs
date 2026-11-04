using System.Globalization;
using PapayApp.Core.Models;
using PapayApp.Core.Services;
using PapayApp.UI.Services;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using PdfSharp.Pdf.IO;

namespace PapayApp.UI.Controls;

public partial class CertificateTemplateEditor : ContentView
{
    private ICertificateTemplateStore? _store;
    private IFileSelectionService? _files;
    private CertificateTemplateSettings? _draft;
    private readonly LayoutDrawable _drawable = new();
    private CertificateElement? _selected;
    private int _pageIndex;
    private int _pageCount;
    private double _pageWidth = 842, _pageHeight = 595;
    private bool _updating, _busy, _dirty, _initialized;
    private PointF? _dragStart;
    private (double X, double Y, double Width, double Height) _original;
    private int _corner = -1;

    public CertificateTemplateEditor()
    {
        InitializeComponent();
        Overlay.Drawable = _drawable;
        FontPicker.ItemsSource = new[] { "Cinzel", "Alex Brush" };
        AlignmentPicker.ItemsSource = new[] { "Izquierda", "Centro", "Derecha" };
        Loaded += async (_, _) =>
        {
            if (_store is not null && !_initialized) await ReloadAsync();
        };
    }

    public void Configure(ICertificateTemplateStore store, IFileSelectionService files)
    {
        _store = store;
        _files = files;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        TemplatePicker.IsEnabled = TemplateNameEntry.IsEnabled = false;
        Workspace.IsEnabled = false;
        LoadButton.IsEnabled = SaveButton.IsEnabled = false;
        try { await action(); }
        catch (Exception exception) { StatusLabel.Text = UserProblem.Describe(exception, "actualizar la plantilla"); }
        finally
        {
            _busy = false;
            TemplatePicker.IsEnabled = TemplateNameEntry.IsEnabled = true;
            Workspace.IsEnabled = true;
            LoadButton.IsEnabled = SaveButton.IsEnabled = true;
        }
    }

    private Task ReloadAsync() => RunAsync(async () =>
    {
        var settings = _store!.Load();
        await OpenDraftAsync(settings);
        _initialized = true;
        _dirty = false;
        RefreshLibrary();
        StatusLabel.Text = "Plantilla activa. Puedes editarla o cargar otro PDF.";
    });

    private void RefreshLibrary()
    {
        if (_store is null) return;
        _updating = true;
        var templates = _store.List();
        TemplatePicker.ItemsSource = templates.ToList();
        TemplatePicker.SelectedItem = templates.FirstOrDefault(t => t.Id == _draft?.Id);
        ActiveTemplateLabel.Text = $"Plantilla activa: {_store.Load().Name}";
        TemplateNameEntry.Text = _draft?.Name ?? string.Empty;
        _updating = false;
    }

    private async void OnTemplateSelected(object? sender, EventArgs e)
    {
        if (_updating || _busy || _store is null || TemplatePicker.SelectedItem is not CertificateTemplateSettings selected) return;
        if (selected.Id == _draft?.Id) return;
        await RunAsync(async () =>
        {
            if (_dirty && !await ConfirmDiscardAsync())
            {
                RefreshLibrary();
                return;
            }
            try
            {
                // Re-read from storage rather than reusing a previously edited picker item.
                var saved = _store.List().First(t => t.Id == selected.Id);
                await OpenDraftAsync(saved);
                _dirty = saved.Id != _store.Load().Id;
                StatusLabel.Text = _dirty ? "Plantilla seleccionada. Pulsa Guardar para activarla." : "Plantilla activa. Sin cambios pendientes.";
            }
            finally { RefreshLibrary(); }
        });
    }

    private void OnTemplateNameEdited(object? sender, TextChangedEventArgs e)
    {
        if (_updating || _busy || _draft is null) return;
        _draft.Name = TemplateNameEntry.Text ?? string.Empty;
        MarkDirty();
    }

    private async Task OpenDraftAsync(CertificateTemplateSettings settings)
    {
        // Validate with the same engine used for generation before replacing the current draft.
        using var pdf = PdfReader.Open(settings.TemplatePath, PdfDocumentOpenMode.Import);
        if (pdf.PageCount == 0) throw new InvalidDataException("El PDF no contiene páginas.");
        var preview = await RenderPageAsync(settings.TemplatePath, 0);
        _draft = settings;
        _pageIndex = 0;
        _pageCount = pdf.PageCount;
        _drawable.Layout = settings.Layout;
        ApplyPreview(preview);
        FileLabel.Text = string.IsNullOrWhiteSpace(settings.OriginalFileName)
            ? Path.GetFileName(settings.TemplatePath) : settings.OriginalFileName;
        Workspace.IsVisible = true;
        RefreshElements();
    }

    private static async Task<(byte[] Bytes, double Width, double Height)> RenderPageAsync(string path, int pageIndex)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
        var document = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
        using var page = document.GetPage((uint)pageIndex);
        using var stream = new InMemoryRandomAccessStream();
        var ratio = Math.Min(1600 / page.Size.Width, 1600 / page.Size.Height);
        await page.RenderToStreamAsync(stream, new PdfPageRenderOptions
        {
            DestinationWidth = (uint)Math.Max(1, page.Size.Width * ratio),
            DestinationHeight = (uint)Math.Max(1, page.Size.Height * ratio)
        });
        stream.Seek(0);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        var bytes = new byte[(int)stream.Size];
        reader.ReadBytes(bytes);
        return (bytes, page.Size.Width, page.Size.Height);
    }

    private void ApplyPreview((byte[] Bytes, double Width, double Height) preview)
    {
        _pageWidth = preview.Width;
        _pageHeight = preview.Height;
        PdfImage.Source = ImageSource.FromStream(() => new MemoryStream(preview.Bytes, writable: false));
        _drawable.PageIndex = _pageIndex;
        PageLabel.Text = $"Página {_pageIndex + 1} de {_pageCount}";
        ResizePreview();
    }

    private async void OnLoadPdf(object? sender, EventArgs e) => await RunAsync(async () =>
    {
        if (_files is null) return;
        if (_dirty && !await ConfirmDiscardAsync()) return;
        var file = await _files.PickSingleFileAsync("Selecciona la plantilla de certificado", [".pdf"]);
        if (file is null) return;
        // Copy to a draft file now, so replacing the source cannot desynchronize the preview and export.
        var draftPath = Path.Combine(FileSystem.CacheDirectory, $"certificate-draft-{Guid.NewGuid():N}.pdf");
        File.Copy(file.FullPath, draftPath);
        try
        {
            await OpenDraftAsync(new CertificateTemplateSettings { TemplatePath = draftPath,
                Name = Path.GetFileNameWithoutExtension(file.FileName), OriginalFileName = file.FileName });
            RefreshLibrary();
            MarkDirty();
        }
        catch { File.Delete(draftPath); throw; }
    });

    private async void OnSave(object? sender, EventArgs e) => await RunAsync(() =>
    {
        if (_draft is null || _store is null) return Task.CompletedTask;
        if (_selected?.Kind == CertificateElementKind.Text &&
            (!TrySize(out _) || !System.Text.RegularExpressions.Regex.IsMatch(ColorEntry.Text ?? "", "^#[0-9a-fA-F]{6}$")))
            throw new ArgumentException("Revisa el tamaño de fuente (6–144) y el color (#RRGGBB).");
        _draft.Layout.Validate(_pageCount);
        _store.Save(_draft);
        var saved = _store.Load();
        _draft.TemplatePath = saved.TemplatePath;
        _draft.Name = saved.Name;
        _draft.OriginalFileName = saved.OriginalFileName;
        _dirty = false;
        RefreshLibrary();
        StatusLabel.Text = "Plantilla guardada. Los próximos certificados usarán este diseño y todas sus páginas.";
        return Task.CompletedTask;
    });

    private async void OnDiscard(object? sender, EventArgs e)
    {
        if (_busy || _store is null) return;
        if (_dirty && !await ConfirmDiscardAsync()) return;
        await ReloadAsync();
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page is not null && await page.DisplayAlertAsync("Cambios sin guardar",
            "Se descartarán los cambios del editor.", "Descartar", "Cancelar");
    }

    private async void OnRestore(object? sender, EventArgs e)
    {
        if (_busy || _store is null) return;
        if (_dirty && !await ConfirmDiscardAsync()) return;
        await RunAsync(async () =>
        {
            await OpenDraftAsync(new CertificateTemplateSettings { TemplatePath = _store.DefaultTemplatePath,
                Name = "Copia del diseño original", OriginalFileName = Path.GetFileName(_store.DefaultTemplatePath) });
            RefreshLibrary();
            MarkDirty();
        });
    }

    private async Task ChangePageAsync(int index)
    {
        if (_draft is null || index < 0 || index >= _pageCount) return;
        await RunAsync(async () =>
        {
            var preview = await RenderPageAsync(_draft.TemplatePath, index);
            _pageIndex = index;
            ApplyPreview(preview);
            RefreshElements();
        });
    }

    private async void OnPreviousPage(object? sender, EventArgs e) => await ChangePageAsync(_pageIndex - 1);
    private async void OnNextPage(object? sender, EventArgs e) => await ChangePageAsync(_pageIndex + 1);

    private void RefreshElements(CertificateElement? selection = null)
    {
        _updating = true;
        var items = _draft?.Layout.Elements.Where(e => e.PageIndex == _pageIndex).ToList() ?? [];
        ElementPicker.ItemsSource = items;
        ElementPicker.SelectedItem = selection ?? items.FirstOrDefault();
        _updating = false;
        SelectElement(ElementPicker.SelectedItem as CertificateElement);
    }

    private void SelectElement(CertificateElement? element)
    {
        _selected = element;
        _drawable.Selected = element;
        _updating = true;
        Inspector.IsVisible = element is not null;
        if (element is not null)
        {
            ElementPicker.SelectedItem = element;
            NameEntry.Text = element.Name;
            InputSwitch.IsToggled = !string.IsNullOrWhiteSpace(element.InputKey);
            ContentEditor.IsEnabled = string.IsNullOrWhiteSpace(element.InputKey);
            TextInspector.IsVisible = element.Kind == CertificateElementKind.Text;
            ContentEditor.Text = element.Content;
            FontPicker.SelectedItem = element.FontFamily;
            FontSizeEntry.Text = element.FontSize.ToString(CultureInfo.CurrentCulture);
            ColorEntry.Text = element.Color;
            BoldSwitch.IsToggled = element.Bold;
            AlignmentPicker.SelectedIndex = (int)element.Alignment;
        }
        _updating = false;
        Overlay.Invalidate();
    }

    private void OnElementSelected(object? sender, EventArgs e)
    {
        if (!_updating) SelectElement(ElementPicker.SelectedItem as CertificateElement);
    }

    private void OnNameUnfocused(object? sender, FocusEventArgs e)
    {
        if (!_updating && _selected is not null) RefreshElements(_selected);
    }

    private void AddElement(CertificateElementKind kind, bool input = false)
    {
        if (_draft is null || _busy) return;
        var element = new CertificateElement { PageIndex = _pageIndex, Kind = kind,
            InputKey = input ? Guid.NewGuid().ToString("N") : string.Empty,
            Name = kind == CertificateElementKind.Text
                ? (input ? $"Texto {_draft.Layout.GenerationInputs.Count(e => e.Kind == CertificateElementKind.Text) + 1}" : "Texto fijo")
                : $"Firma {_draft.Layout.Elements.Count(e => e.Kind == CertificateElementKind.Signature) + 1}" };
        _draft.Layout.Elements.Add(element);
        RefreshElements(element);
        MarkDirty();
    }

    private void OnAddText(object? sender, EventArgs e) => AddElement(CertificateElementKind.Text, true);
    private void OnAddFixedText(object? sender, EventArgs e) => AddElement(CertificateElementKind.Text);
    private void OnAddSignature(object? sender, EventArgs e) => AddElement(CertificateElementKind.Signature, true);
    private void OnInputToggled(object? sender, ToggledEventArgs e)
    {
        if (_updating || _selected is null) return;
        _selected.InputKey = e.Value ? (string.IsNullOrWhiteSpace(_selected.InputKey) ? Guid.NewGuid().ToString("N") : _selected.InputKey) : string.Empty;
        ContentEditor.IsEnabled = !e.Value;
        MarkDirty();
    }
    private void OnAddSigner(object? sender, EventArgs e)
    {
        if (_draft is null || _busy) return;
        var number = _draft.Layout.Elements.Count(e => e.Kind == CertificateElementKind.Signature) + 1;
        var signature = new CertificateElement { Name = $"Firma del firmante {number}", Kind = CertificateElementKind.Signature,
            InputKey = Guid.NewGuid().ToString("N"), PageIndex = _pageIndex, X = .12, Y = .64, Width = .27, Height = .14 };
        _draft.Layout.Elements.Add(signature);
        _draft.Layout.Elements.Add(new() { Name = $"Nombre del firmante {number}", InputKey = Guid.NewGuid().ToString("N"),
            PageIndex = _pageIndex, X = .1, Y = .81, Width = .31, Height = .05, FontSize = 14, Bold = true });
        _draft.Layout.Elements.Add(new() { Name = $"Cargo del firmante {number}", InputKey = Guid.NewGuid().ToString("N"),
            PageIndex = _pageIndex, X = .1, Y = .87, Width = .31, Height = .05, FontSize = 10 });
        RefreshElements(signature);
        MarkDirty();
    }
    private void OnDelete(object? sender, EventArgs e)
    {
        if (_selected is null || _draft is null || _busy) return;
        _draft.Layout.Elements.Remove(_selected);
        RefreshElements();
        MarkDirty();
    }

    private bool TrySize(out double size) => double.TryParse(FontSizeEntry.Text, NumberStyles.Float,
        CultureInfo.CurrentCulture, out size) && double.IsFinite(size) && size >= 6 && size <= 144;

    private void OnTextEdited(object? sender, TextChangedEventArgs e) => UpdateElement();
    private void OnFormatEdited(object? sender, EventArgs e) => UpdateElement();
    private void OnBoldEdited(object? sender, ToggledEventArgs e) => UpdateElement();

    private void UpdateElement()
    {
        if (_updating || _selected is null) return;
        _selected.Name = NameEntry.Text ?? "";
        _selected.Content = ContentEditor.Text ?? "";
        _selected.FontFamily = FontPicker.SelectedItem as string ?? "Cinzel";
        // Retain invalid size as NaN so validation cannot silently save a previous value.
        _selected.FontSize = TrySize(out var size) ? size : double.NaN;
        _selected.Color = ColorEntry.Text ?? "";
        _selected.Bold = BoldSwitch.IsToggled;
        _selected.Alignment = (CertificateTextAlignment)Math.Max(0, AlignmentPicker.SelectedIndex);
        MarkDirty();
    }

    private void MarkDirty()
    {
        _dirty = true;
        StatusLabel.Text = "Cambios sin guardar.";
        Overlay.Invalidate();
    }

    private void OnPreviewSizeChanged(object? sender, EventArgs e) => ResizePreview();
    private void ResizePreview()
    {
        if (PreviewHost.Width <= 0) return;
        var width = Math.Min(PreviewHost.Width, 1100);
        PageCanvas.WidthRequest = width;
        PageCanvas.HeightRequest = width * _pageHeight / _pageWidth;
        Overlay.Invalidate();
    }

    private void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        if (_busy || _draft is null || e.Touches.Length == 0 || Overlay.Width <= 0 || Overlay.Height <= 0) return;
        var point = e.Touches[0];
        _corner = _selected is null ? -1 : HitCorner(_selected, point);
        var element = _corner >= 0 ? _selected : _draft.Layout.Elements.LastOrDefault(element =>
            element.PageIndex == _pageIndex && ElementBounds(element).Contains(point));
        SelectElement(element);
        if (element is null) return;
        _dragStart = point;
        _original = (element.X, element.Y, element.Width, element.Height);
    }

    private RectF ElementBounds(CertificateElement e) => new((float)(e.X * Overlay.Width), (float)(e.Y * Overlay.Height),
        (float)(e.Width * Overlay.Width), (float)(e.Height * Overlay.Height));

    private int HitCorner(CertificateElement element, PointF point)
    {
        var rect = ElementBounds(element);
        PointF[] corners = [new(rect.Left, rect.Top), new(rect.Right, rect.Top), new(rect.Left, rect.Bottom), new(rect.Right, rect.Bottom)];
        for (var i = 0; i < corners.Length; i++)
            if (Math.Abs(point.X - corners[i].X) <= 12 && Math.Abs(point.Y - corners[i].Y) <= 12) return i;
        return -1;
    }

    private void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        if (_selected is null || _dragStart is null || e.Touches.Length == 0) return;
        var dx = (e.Touches[0].X - _dragStart.Value.X) / Overlay.Width;
        var dy = (e.Touches[0].Y - _dragStart.Value.Y) / Overlay.Height;
        if (_corner < 0)
        {
            _selected.X = Math.Clamp(_original.X + dx, 0, 1 - _original.Width);
            _selected.Y = Math.Clamp(_original.Y + dy, 0, 1 - _original.Height);
        }
        else
        {
            const double minimum = .015;
            var left = _original.X;
            var top = _original.Y;
            var right = left + _original.Width;
            var bottom = top + _original.Height;
            if (_corner is 0 or 2) left = Math.Clamp(left + dx, 0, right - Math.Min(minimum, _original.Width));
            else right = Math.Clamp(right + dx, left + Math.Min(minimum, _original.Width), 1);
            if (_corner is 0 or 1) top = Math.Clamp(top + dy, 0, bottom - Math.Min(minimum, _original.Height));
            else bottom = Math.Clamp(bottom + dy, top + Math.Min(minimum, _original.Height), 1);
            _selected.X = left; _selected.Y = top;
            _selected.Width = right - left; _selected.Height = bottom - top;
        }
        MarkDirty();
    }

    private void OnEndInteraction(object? sender, TouchEventArgs e) => _dragStart = null;
    private void OnCancelInteraction(object? sender, EventArgs e) => _dragStart = null;

    private sealed class LayoutDrawable : IDrawable
    {
        public CertificateLayout? Layout { get; set; }
        public CertificateElement? Selected { get; set; }
        public int PageIndex { get; set; }
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (Layout is null) return;
            foreach (var e in Layout.Elements.Where(e => e.PageIndex == PageIndex))
            {
                var rect = new RectF((float)e.X * dirtyRect.Width, (float)e.Y * dirtyRect.Height,
                    (float)e.Width * dirtyRect.Width, (float)e.Height * dirtyRect.Height);
                var color = ReferenceEquals(e, Selected) ? Color.FromArgb("#D96616") :
                    e.Kind == CertificateElementKind.Signature ? Color.FromArgb("#7C3AED") : Color.FromArgb("#1269A6");
                canvas.FillColor = color.WithAlpha(.15f);
                canvas.FillRectangle(rect);
                canvas.StrokeColor = color;
                canvas.StrokeSize = ReferenceEquals(e, Selected) ? 3 : 1.5f;
                canvas.DrawRectangle(rect);
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 12;
                canvas.SaveState();
                canvas.ClipRectangle(rect.X, rect.Y, rect.Width, rect.Height);
                canvas.FillColor = Colors.White.WithAlpha(.85f);
                canvas.FillRectangle(rect.X, rect.Y, rect.Width, Math.Min(20, rect.Height));
                canvas.DrawString(e.Name, rect.X + 4, rect.Y, Math.Max(0, rect.Width - 8), Math.Min(20, rect.Height),
                    HorizontalAlignment.Left, VerticalAlignment.Center);
                canvas.RestoreState();
                if (!ReferenceEquals(e, Selected)) continue;
                canvas.FillColor = color;
                foreach (var x in new[] { rect.Left, rect.Right })
                    foreach (var y in new[] { rect.Top, rect.Bottom }) canvas.FillRectangle(x - 5, y - 5, 10, 10);
            }
        }
    }
}
