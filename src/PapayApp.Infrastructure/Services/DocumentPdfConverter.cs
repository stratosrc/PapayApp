using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class DocumentPdfConverter : IDocumentPdfConverter
{
    public IDocumentPdfSession OpenSession()
    {
        Exception? wordError = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var wordType = Type.GetTypeFromProgID("Word.Application");
                if (wordType is not null) return new WordSession(wordType);
            }
            catch (Exception e) { wordError = e; }
        }
        var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
        var candidates = roots.Where(Directory.Exists).Select(r => Path.Combine(r, "LibreOffice", "program", "soffice.exe"))
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(Directory.Exists).Select(r => Path.Combine(r, OperatingSystem.IsWindows() ? "soffice.exe" : "soffice")));
        var executable = candidates.FirstOrDefault(File.Exists);
        if (executable is not null) return new LibreOfficeSession(executable);
        throw new InvalidOperationException("Para generar PDF desde las plantillas Word, instala y abre Microsoft Word o LibreOffice una vez, cierra sus avisos iniciales y vuelve a generar. " + (wordError is null ? "No se encontró un conversor disponible." : "Word no pudo iniciarse: " + wordError.Message), wordError);
    }

    private abstract class Session : IDocumentPdfSession
    {
        protected readonly string DirectoryPath = Directory.CreateTempSubdirectory("PapayApp-LabPdf-").FullName;
        public byte[] Convert(byte[] document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = Path.Combine(DirectoryPath, "informe.docx");
            var output = Path.Combine(DirectoryPath, "informe.pdf");
            try
            {
                File.WriteAllBytes(input, document);
                Export(input, output, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var pdf = File.ReadAllBytes(output);
                if (pdf.Length < 5 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
                    throw new InvalidDataException("El conversor no produjo un PDF válido. Revisa la plantilla en Word o LibreOffice.");
                return pdf;
            }
            finally { File.Delete(input); File.Delete(output); }
        }
        protected abstract void Export(string input, string output, CancellationToken cancellationToken);
        public virtual void Dispose()
        {
            // Only remove this session's private temporary workspace.
            var root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(DirectoryPath).StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                try { Directory.Delete(DirectoryPath, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private sealed class WordSession : Session
    {
        private object? _application, _documents;
        public WordSession(Type wordType)
        {
            try
            {
                _application = Activator.CreateInstance(wordType) ?? throw new InvalidOperationException("No se pudo iniciar Microsoft Word.");
                dynamic word = _application;
                word.Visible = false;
                word.DisplayAlerts = 0;
                word.AutomationSecurity = 3; // Force-disable macros in supplied templates.
                _documents = word.Documents;
            }
            catch { Dispose(); throw; }
        }
        protected override void Export(string input, string output, CancellationToken cancellationToken)
        {
            object? document = null;
            try
            {
                dynamic documents = _documents!;
                document = documents.Open(FileName: input, ConfirmConversions: false, ReadOnly: true, AddToRecentFiles: false, Visible: false);
                dynamic report = document;
                report.ExportAsFixedFormat(OutputFileName: output, ExportFormat: 17, OpenAfterExport: false);
            }
            finally
            {
                if (document is not null)
                {
                    try { ((dynamic)document).Close(SaveChanges: 0); }
                    finally { Marshal.FinalReleaseComObject(document); }
                }
            }
        }
        public override void Dispose()
        {
            try
            {
                if (_documents is not null) { Marshal.FinalReleaseComObject(_documents); _documents = null; }
                if (_application is not null)
                {
                    try { ((dynamic)_application).Quit(SaveChanges: 0); }
                    catch (COMException) { }
                    finally { Marshal.FinalReleaseComObject(_application); _application = null; }
                }
            }
            finally { base.Dispose(); }
        }
    }

    private sealed class LibreOfficeSession(string executable) : Session
    {
        protected override void Export(string input, string output, CancellationToken cancellationToken)
        {
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "-env:UserInstallation=" + new Uri(Path.Combine(DirectoryPath, "profile")).AbsoluteUri,
                "--headless", "--convert-to", "pdf:writer_pdf_Export", "--outdir", DirectoryPath, input }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new IOException("No se pudo iniciar LibreOffice para convertir a PDF.");
            var timer = Stopwatch.StartNew();
            try
            {
                while (!process.WaitForExit(200))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (timer.Elapsed > TimeSpan.FromMinutes(2)) throw new IOException("LibreOffice tardó demasiado al convertir a PDF. Revisa la plantilla y vuelve a intentarlo.");
                }
                if (process.ExitCode != 0 || !File.Exists(output)) throw new IOException("LibreOffice no generó el PDF. Abre la plantilla en LibreOffice, comprueba su instalación y vuelve a intentarlo.");
            }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        }
    }
}
