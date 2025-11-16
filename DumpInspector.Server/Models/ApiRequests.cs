using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace DumpInspector.Server.Models
{
    /// <summary>
    /// 사용자 회원가입 요청 페이로드.
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>
        /// 생성할 사용자 이름(로그인 ID).
        /// </summary>
        public string Username { get; set; } = default!;

        /// <summary>
        /// 사용자 비밀번호.
        /// </summary>
        public string Password { get; set; } = default!;
    }

    /// <summary>
    /// 로그인 자격 증명 검증 요청.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// 로그인할 사용자 이름.
        /// </summary>
        public string Username { get; set; } = default!;

        /// <summary>
        /// 사용자 비밀번호.
        /// </summary>
        public string Password { get; set; } = default!;
    }

    /// <summary>
    /// 비밀번호 변경 요청 DTO.
    /// </summary>
    public class ChangePasswordRequest
    {
        /// <summary>
        /// 비밀번호를 변경할 사용자 이름.
        /// </summary>
        public string Username { get; set; } = default!;

        /// <summary>
        /// 기존 비밀번호.
        /// </summary>
        public string OldPassword { get; set; } = default!;

        /// <summary>
        /// 새 비밀번호.
        /// </summary>
        public string NewPassword { get; set; } = default!;
    }

    /// <summary>
    /// 관리자 패널에서 사용자 생성을 위한 요청.
    /// </summary>
    public class CreateUserRequest
    {
        /// <summary>
        /// 생성할 사용자 이름(로그인 ID).
        /// </summary>
        public string Username { get; set; } = default!;

        /// <summary>
        /// 알림 및 계정 안내를 보낼 이메일.
        /// </summary>
        public string Email { get; set; } = default!;
    }

    /// <summary>
    /// 비밀번호 강제 초기화 요청.
    /// </summary>
    public class ForceResetRequest
    {
        /// <summary>
        /// 임시 비밀번호를 발급할 사용자 이름.
        /// </summary>
        public string Username { get; set; } = default!;
    }

    /// <summary>
    /// PDB 파일 업로드 요청.
    /// </summary>
    public class PdbUploadRequest
    {
        /// <summary>
        /// 업로드할 PDB 파일(.pdb 확장자).
        /// </summary>
        [Required]
        public IFormFile File { get; set; } = default!;

        /// <summary>
        /// SymStore에 기록할 제품 이름(비우면 기본값 사용).
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// 버전 정보.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// 업로드 코멘트.
        /// </summary>
        public string? Comment { get; set; }

        /// <summary>
        /// 업로더 정보.
        /// </summary>
        public string? UploadedBy { get; set; }
    }
}
