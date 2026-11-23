namespace PapayApp.Core.Services;

public interface IDocumentPdfConverter
{
    IDocumentPdfSession OpenSession();
}

public interface IDocumentPdfSession : IDisposable
{
    byte[] Convert(byte[] document, CancellationToken cancellationToken);
}
