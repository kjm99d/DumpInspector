using System.Text.Json.Serialization;

namespace SymbolAutoUpload;

public record CredentialValidationResponse([property: JsonPropertyName("valid")] bool Valid);

public record PdbUploadResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("result")] PdbUploadResult Result);

public record PdbUploadResult(
    [property: JsonPropertyName("symbolStoreRoot")] string SymbolStoreRoot,
    [property: JsonPropertyName("product")] string Product,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("originalFileName")] string OriginalFileName,
    [property: JsonPropertyName("symStoreCommand")] string SymStoreCommand,
    [property: JsonPropertyName("symStoreOutput")] string SymStoreOutput);
