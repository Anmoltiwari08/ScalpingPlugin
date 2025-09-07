namespace TestScalpingBackend.Models;

public class JwtSettings
{
    public required string Key { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int ExpireMinutes { get; set; }
}

public partial class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}


