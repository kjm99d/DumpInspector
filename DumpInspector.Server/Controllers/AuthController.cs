using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DumpInspector.Server.Controllers
{
    /// <summary>
    /// 간단한 인증 관련 보조 API를 제공한다.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        /// <summary>
        /// 셀프 회원가입을 시도하지만 현재는 비활성화되어 있다.
        /// </summary>
        [HttpPost("register")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status405MethodNotAllowed)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            return StatusCode(StatusCodes.Status405MethodNotAllowed, "Self-registration is disabled");
        }

        /// <summary>
        /// 지정한 사용자가 관리자 권한인지 확인한다.
        /// </summary>
        [HttpGet("is-admin/{username}")]
        [ProducesResponseType(typeof(AdminStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> IsAdmin(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return BadRequest();
            var isAdmin = await _auth.IsAdminAsync(username);
            return Ok(new AdminStatusResponse(isAdmin));
        }

        /// <summary>
        /// 사용자 자격 증명 검증.
        /// </summary>
        [HttpPost("validate")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(CredentialValidationResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Validate([FromBody] LoginRequest req)
        {
            var ok = await _auth.ValidateCredentialsAsync(req.Username, req.Password);
            return Ok(new CredentialValidationResponse(ok));
        }

        /// <summary>
        /// 사용자 비밀번호 변경.
        /// </summary>
        [HttpPost("change-password")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var ok = await _auth.ValidateCredentialsAsync(req.Username, req.OldPassword);
            if (!ok) return Unauthorized();
            await _auth.ResetPasswordAsync(req.Username, req.NewPassword);
            return Ok();
        }
    }

}
