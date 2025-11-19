namespace SymbolAutoUpload;

public class SymbolUploadSettings
{
    public const string SectionName = "SymbolUpload";

    /// <summary>
    /// DumpInspector.Server API의 기본 URL.
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 기본 로그인 아이디(비우면 콘솔에서 입력).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 기본 로그인 비밀번호(비우면 콘솔에서 입력).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 업로드 시 사용할 제품명.
    /// </summary>
    public string? ProductName { get; set; } = string.Empty;

    /// <summary>
    /// 기본 버전 문자열.
    /// </summary>
    public string? Version { get; set; } = string.Empty;

    /// <summary>
    /// 업로드 코멘트 기본값.
    /// </summary>
    public string? Comment { get; set; } = "SymbolAutoUpload";

    /// <summary>
    /// PDB 검색 시 하위 폴더까지 포함할지 여부.
    /// </summary>
    public bool IncludeSubdirectories { get; set; } = true;

    /// <summary>
    /// 기본 검색 경로(비우면 바이너리 폴더 사용).
    /// </summary>
    public string? PdbDirectory { get; set; }

    /// <summary>
    /// UploadedBy 필드를 상시 특정 값으로 지정할 경우 사용.
    /// </summary>
    public string? UploadedByOverride { get; set; }

    /// <summary>
    /// HTTP 타임아웃(초).
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 300;
}
