
using TestScalpingBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace TestScalpingBackend.Middleware;

public class JwtAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly string _jwtSecret;
    private readonly ILogger<JwtAuthorizationFilter> _logger;
    private readonly AppDbContext _dbContext;

    public JwtAuthorizationFilter(IConfiguration configuration, AppDbContext dbContext, ILogger<JwtAuthorizationFilter> logger)
    {
        _jwtSecret = configuration["JwtSettings:Key"] ?? "";
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            var request = context.HttpContext.Request;
            Console.WriteLine("Request URL: " + request.Path);

            var token = request.Cookies["jwtToken"] ??
                        request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

            Console.WriteLine("Token: " + token);

            if (string.IsNullOrEmpty(token))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Unauthorized request" });
                Console.WriteLine("Unauthorized request");
                return;
            }

            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "YourApp",
                ValidAudience = "YourAppUsers",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret))
            };

            var principal = handler.ValidateToken(token, validationParameters, out _);
            var userId = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Invalid token" });
                return;
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);

            if (user == null)
            {
                context.Result = new UnauthorizedObjectResult(new { message = "User not found" });
                return;
            }

            context.HttpContext.Items["User"] = user;
        }
        catch (Exception ex)
        {
            _logger.LogError($"JWT Filter Error: {ex.Message}");
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid access token" });
        }
    }

}

