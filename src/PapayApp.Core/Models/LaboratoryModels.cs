namespace PapayApp.Core.Models;

public sealed record LaboratoryPatient(int RowNumber, string FullName, string Identification, int Age, string Gender, string TestResult)
{
    public string Sex => Gender.Equals("M", StringComparison.OrdinalIgnoreCase) ? "MASCULINO" : "FEMENINO";
}

public sealed class LaboratoryImportResult
{
    public IReadOnlyList<LaboratoryPatient> Patients { get; init; } = [];
    public IReadOnlyList<string> Issues { get; init; } = [];
}

public sealed class LaboratoryTemplateSettings
{
    public string ToxicologyName { get; set; } = "Resultados toxicológicos.docx";
    public string SerologyName { get; set; } = "Resultados serológicos.docx";
    public required string ToxicologyPath { get; set; }
    public required string SerologyPath { get; set; }
}

public sealed class LaboratoryGenerationRequest
{
    public required LaboratoryTemplateSettings Templates { get; init; }
    public required string BaseDirectory { get; init; }
    public required IReadOnlyList<LaboratoryPatient> Patients { get; init; }
    public DateTime ReportDate { get; init; } = DateTime.Today;
    public bool AutomaticToxicology { get; init; }
    public bool OverwriteConfirmed { get; init; }
}

public sealed record LaboratoryFileResult(LaboratoryPatient Patient, string? ToxicologyPath, string? SerologyPath, string? Error)
{
    public bool Success => Error is null;
}

public sealed class LaboratoryGenerationResult
{
    public required string OutputRoot { get; init; }
    public IReadOnlyList<LaboratoryFileResult> Files { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string? LogPath { get; init; }
}
