namespace Tempest.Core.EngineeringDomain;

public sealed class ValidationDiagnostic : IValidationDiagnostic
{
    public string Code { get; }
    public string Message { get; }
    public Guid? SubjectId { get; }

    public ValidationDiagnostic(string code, string message, Guid? subjectId = null)
    {
        Code = code;
        Message = message;
        SubjectId = subjectId;
    }
}

public sealed class ValidationResult : IValidationResult
{
    public static readonly ValidationResult Valid = new(Array.Empty<IValidationDiagnostic>(), Array.Empty<IValidationDiagnostic>());

    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<IValidationDiagnostic> Errors { get; }
    public IReadOnlyList<IValidationDiagnostic> Warnings { get; }

    public ValidationResult(IReadOnlyList<IValidationDiagnostic> errors, IReadOnlyList<IValidationDiagnostic> warnings)
    {
        Errors = errors;
        Warnings = warnings;
    }

    public static ValidationResult SingleError(string code, string message, Guid? subjectId = null) =>
        new(new IValidationDiagnostic[] { new ValidationDiagnostic(code, message, subjectId) }, Array.Empty<IValidationDiagnostic>());
}
