using ProductApi1.Models;
using ProductApi1.Services;
using Microsoft.AspNetCore.Mvc;

namespace ProductApi1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var token = await _authService.LoginAsync(model);

            if (token == null)
            {
                return Unauthorized(new { Message = "Invalid username or password" });
            }

            return Ok(new { Token = token, Message = "Login successfully!!!" });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.RegisterUserAsync(model);

            if (result)
            {
                return Ok(new { Message = "User registered successfully" });
            }

            return BadRequest(new { Message = "User registration failed" });
        }
    }
}