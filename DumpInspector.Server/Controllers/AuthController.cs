using DumpInspector.Server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DumpInspector.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            return StatusCode(StatusCodes.Status405MethodNotAllowed, "Self-registration is disabled");
        }

        [HttpGet("is-admin/{username}")]
        public async Task<IActionResult> IsAdmin(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return BadRequest();
            var isAdmin = await _auth.IsAdminAsync(username);
            return Ok(new { isAdmin });
        }

        [HttpPost("validate")]
        public async Task<IActionResult> Validate([FromBody] LoginRequest req)
        {
            var ok = await _auth.ValidateCredentialsAsync(req.Username, req.Password);
            return Ok(new { valid = ok });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var ok = await _auth.ValidateCredentialsAsync(req.Username, req.OldPassword);
            if (!ok) return Unauthorized();
            await _auth.ResetPasswordAsync(req.Username, req.NewPassword);
            return Ok();
        }
    }

    public record RegisterRequest(string Username, string Password);
    public record LoginRequest(string Username, string Password);
}

public record ChangePasswordRequest(string Username, string OldPassword, string NewPassword);
