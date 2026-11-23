using System.Globalization;
using PapayApp.Core.Models;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class LaboratoryGenerationService(ISafeFileNameService names, IDocumentPdfConverter converter) : ILaboratoryGenerationService
{
    private sealed record Output(LaboratoryPatient Patient, string Toxicology, string Serology);
    private static string Root(LaboratoryGenerationRequest request) => Path.Combine(request.BaseDirectory, "PRUEBAS", request.ReportDate.ToString("ddMMyyyy", CultureInfo.InvariantCulture));
    private IReadOnlyList<Output> Plan(LaboratoryGenerationRequest request)
    {
        var directoryNames = request.Patients.Select(p => (Patient: p, Name: names.SanitizePathSegment(p.FullName, "Paciente"))).ToList();
        var collisions = directoryNames.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return directoryNames.Select(p =>
        {
            var folder = Path.Combine(Root(request), collisions.Contains(p.Name) ? $"{p.Name} - {p.Patient.Identification}" : p.Name);
            var patientName = names.SanitizePathSegment(p.Patient.FullName.ToUpperInvariant(), "PACIENTE");
            return new Output(p.Patient,
                Path.Combine(folder, $"RESULTADO TOXICOLÓGICO EN ORINA -  {patientName}.pdf"),
                Path.Combine(folder, $"RESULTADO SEROLÓGICO DE VIH -  {patientName}.pdf"));
        }).ToList();
    }
    public IReadOnlyList<string> ExistingFiles(LaboratoryGenerationRequest request) => Plan(request).SelectMany(p => new[] { p.Toxicology, p.Serology }).Where(File.Exists).ToList();
    public IReadOnlyList<string> ReviewWarnings(LaboratoryGenerationRequest request)
    {
        if (!request.AutomaticToxicology) return [];
        if (!LaboratoryDocxTemplate.SupportsAutomatic(request.Templates.ToxicologyPath))
            return ["La plantilla toxicológica no contiene los siete marcadores de resultados. Se conservarán los valores por defecto en todos los toxicológicos; revísalos manualmente."];
        return request.Patients.Where(p => LaboratoryTestInterpreter.Interpret(p.TestResult) is null)
            .Select(p => $"Fila {p.RowNumber}, {p.FullName}: TEST vacío, inválido o no reconocido ('{p.TestResult}'). Se conservarán los valores por defecto; revisión manual necesaria.").ToList();
    }

    public Task<LaboratoryGenerationResult> GenerateAsync(LaboratoryGenerationRequest request, IProgress<string>? progress = null, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        if (!Directory.Exists(request.BaseDirectory)) throw new DirectoryNotFoundException("Configura una carpeta base disponible en Ajustes.");
        if (request.Patients.Count == 0) throw new ArgumentException("Selecciona al menos un paciente.");
        if (request.Patients.Select(p => p.Identification).Distinct().Count() != request.Patients.Count) throw new ArgumentException("No se puede generar dos veces la misma cédula en un lote.");
        foreach (var p in request.Patients)
            if (string.IsNullOrWhiteSpace(p.FullName) || p.Identification.Length != 10 || p.Identification.Any(c => !char.IsAsciiDigit(c)) || p.Gender is not ("M" or "F") || p.Age < 0 || p.Age > 120)
                throw new ArgumentException($"Revisa los datos del paciente de la fila {p.RowNumber}.");
        LaboratoryDocxTemplate.Validate(request.Templates.ToxicologyPath);
        LaboratoryDocxTemplate.Validate(request.Templates.SerologyPath);
        var plan = Plan(request);
        if (!request.OverwriteConfirmed && plan.SelectMany(p => new[] { p.Toxicology, p.Serology }).Any(File.Exists))
            throw new IOException("Existen documentos para esta fecha y paciente. Confirma su reemplazo antes de generar.");
        var automatic = request.AutomaticToxicology && LaboratoryDocxTemplate.SupportsAutomatic(request.Templates.ToxicologyPath);
        var warnings = ReviewWarnings(request).ToList();
        var files = new List<LaboratoryFileResult>();
        using var pdf = converter.OpenSession();
        foreach (var item in plan)
        {
            if (cancellationToken.IsCancellationRequested) { warnings.Add($"Lote detenido: se completaron {files.Count} de {plan.Count} pacientes."); break; }
            progress?.Report($"Generando {files.Count + 1}/{plan.Count}: {item.Patient.FullName}");
            try
            {
                var values = automatic ? LaboratoryTestInterpreter.Interpret(item.Patient.TestResult) : null;
                var toxicology = LaboratoryDocxTemplate.Fill(request.Templates.ToxicologyPath, item.Patient, request.ReportDate, values);
                // Serology always preserves the template's manual results.
                var serology = LaboratoryDocxTemplate.Fill(request.Templates.SerologyPath, item.Patient, request.ReportDate, null);
                var toxicologyPdf = pdf.Convert(toxicology, cancellationToken);
                var serologyPdf = pdf.Convert(serology, cancellationToken);
                Directory.CreateDirectory(Path.GetDirectoryName(item.Toxicology)!);
                SavePair(item.Toxicology, toxicologyPdf, item.Serology, serologyPdf, request.OverwriteConfirmed);
                files.Add(new(item.Patient, item.Toxicology, item.Serology, null));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                warnings.Add($"Lote detenido: se completaron {files.Count} de {plan.Count} pacientes.");
                break;
            }
            catch (Exception e)
            {
                files.Add(new(item.Patient, null, null, e.Message));
            }
        }
        var logLines = warnings.Concat(files.Where(f => !f.Success).Select(f => $"ERROR · Fila {f.Patient.RowNumber} · {f.Patient.FullName}: {f.Error}")).ToList();
        string? log = null;
        if (logLines.Count > 0)
        {
            Directory.CreateDirectory(Root(request));
            log = names.EnsureUniqueFilePath(Root(request), $"revision_laboratorio_{DateTime.Now:HHmmss}", ".txt");
            try { File.WriteAllLines(log, logLines); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                warnings.Add("No se pudo guardar el registro de revisión: " + e.Message);
                log = null;
            }
        }
        return new LaboratoryGenerationResult { OutputRoot = Root(request), Files = files, Warnings = warnings, LogPath = log };
    }, cancellationToken);

    private static void SavePair(string firstPath, byte[] first, string secondPath, byte[] second, bool overwrite)
    {
        // Render both first; stage both files and restore the previous pair if either write fails.
        var suffix = "." + Guid.NewGuid().ToString("N") + ".tmp";
        var a = firstPath + suffix; var b = secondPath + suffix;
        var oldA = File.Exists(firstPath) ? File.ReadAllBytes(firstPath) : null;
        var oldB = File.Exists(secondPath) ? File.ReadAllBytes(secondPath) : null;
        var wroteA = false; var wroteB = false;
        try
        {
            File.WriteAllBytes(a, first); File.WriteAllBytes(b, second);
            File.Move(a, firstPath, overwrite); wroteA = true;
            File.Move(b, secondPath, overwrite); wroteB = true;
        }
        catch
        {
            if (wroteA) { if (oldA is null) File.Delete(firstPath); else File.WriteAllBytes(firstPath, oldA); }
            if (wroteB) { if (oldB is null) File.Delete(secondPath); else File.WriteAllBytes(secondPath, oldB); }
            throw;
        }
        finally { File.Delete(a); File.Delete(b); }
    }
}
