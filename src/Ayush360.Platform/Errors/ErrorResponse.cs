namespace Ayush360.Platform.Errors;

/// <summary>
/// The one error shape every Ayush360 API returns. The React app shows <see cref="Message"/>
/// and puts each entry of <see cref="Errors"/> next to the matching form field.
/// </summary>
public sealed record ErrorResponse(
    string Message,
    IReadOnlyDictionary<string, string>? Errors,
    string CorrelationId);
