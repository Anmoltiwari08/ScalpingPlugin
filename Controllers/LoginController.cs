using TestScalpingBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TestScalpingBackend.Middleware;

namespace TestScalpingBackend.Controllers
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private AuthService _authService;
        private readonly ILogger<UserController> _logger;

        public UserController(AppDbContext context, AuthService authService, ILogger<UserController> logger)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            _logger.LogInformation("Login request received {@LoginRequest}", loginRequest);

            if (string.IsNullOrWhiteSpace(loginRequest.Email) || string.IsNullOrWhiteSpace(loginRequest.Password))
            {
                _logger.LogError("Email and password are required.");
                return BadRequest("Email and password are required.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginRequest.Email.Trim());

            if (user == null || !user.IsPasswordCorrect(loginRequest.Password.Trim()))
            {
                _logger.LogError("Invalid credentials");
                return Unauthorized("Invalid credentials");
            }

            var token = _authService.GenerateJwtToken(user);

            Response.Cookies.Append("jwtToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddHours(24)
            });

            _logger.LogInformation("Login successful {@User}", user);

            return Ok(new { message = "Login successful", user });
        }

        [HttpGet("logout")]
        [ServiceFilter(typeof(JwtAuthorizationFilter))]
        public IActionResult Logout()
        {
            _logger.LogInformation("Logout request received");

            Response.Cookies.Delete("jwtToken");

            _logger.LogInformation("Logged out successfully");

            return Ok(new { message = "Logged out successfully", Status = true });
        }

        [HttpGet("GetUser")]
        [ServiceFilter(typeof(JwtAuthorizationFilter))]
        public IActionResult GetUser()
        {
            var user = HttpContext.Items["User"] as User;

            _logger.LogInformation("User found successfully {@User}", user);

            return new JsonResult(new
            {
                message = "User found successfully",
                user
            });
        }
   
    }

}

