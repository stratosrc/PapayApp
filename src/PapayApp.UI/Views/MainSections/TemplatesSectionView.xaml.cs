using PapayApp.Core.Services;
using PapayApp.UI.Services;
namespace PapayApp.UI.Views.MainSections;
public partial class TemplatesSectionView : ContentView
{
    public TemplatesSectionView() { InitializeComponent(); }
    public void ConfigureCertificateEditor(ICertificateTemplateStore store, IFileSelectionService files) => CertificateEditor.Configure(store, files);
}