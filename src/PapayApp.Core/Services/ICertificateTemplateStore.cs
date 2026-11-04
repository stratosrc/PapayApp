using PapayApp.Core.Models;

namespace PapayApp.Core.Services;

public interface ICertificateTemplateStore
{
    CertificateTemplateSettings Load();
    IReadOnlyList<CertificateTemplateSettings> List();
    void Save(CertificateTemplateSettings settings);
    string DefaultTemplatePath { get; }
}
