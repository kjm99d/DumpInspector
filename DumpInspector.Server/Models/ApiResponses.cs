namespace DumpInspector.Server.Models
{
    /// <summary>
    /// 덤프 업로드 후 세션 정보를 제공한다.
    /// </summary>
    public record DumpUploadAcceptedResponse(string SessionId, string FileName, long SizeBytes);

    /// <summary>
    /// 저장된 덤프 파일의 간단한 요약.
    /// </summary>
    public record DumpFileEntry(string Name, long Size);

    /// <summary>
    /// 인증 여부 응답.
    /// </summary>
    public record CredentialValidationResponse(bool Valid);

    /// <summary>
    /// 관리자 여부 응답.
    /// </summary>
    public record AdminStatusResponse(bool IsAdmin);

    /// <summary>
    /// 사용자 생성 결과.
    /// </summary>
    public record CreateUserResponse(bool Success, string? TemporaryPassword);

    /// <summary>
    /// 비밀번호 초기화 결과.
    /// </summary>
    public record ForceResetResponse(string TemporaryPassword);

    /// <summary>
    /// PDB 업로드 결과 메시지.
    /// </summary>
    public record PdbUploadResponse(string Message, PdbUploadResult Result);
}
