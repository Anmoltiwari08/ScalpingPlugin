using TestScalpingBackend.Services;
using Microsoft.EntityFrameworkCore;
using DictionaryExample;
using Serilog;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using System.Security.Claims;
using TestScalpingBackend.Models;
using TestScalpingBackend.Middleware;
using WebServicesApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,   // new file every day
        retainedFileCountLimit: 30,             // keep last 30 files (approx 1 month)
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] ({ThreadId}) {Message}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5065);
});

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<MT5Connection>();
builder.Services.AddSingleton<SymbolStore>();
builder.Services.AddSingleton<DealSubscribe>();
builder.Services.AddScoped<MT5Operations>();
builder.Services.AddScoped<ScalpingDeduction>();

builder.Services.AddScoped<JwtAuthorizationFilter>();
builder.Services.AddScoped<AuthService>();

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
                  ?? throw new Exception("JWT settings missing");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy.WithOrigins(
                         //   "http://41.215.240.85:3002"
                         "http://localhost:3002"
                                     )
                        .AllowCredentials()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();
app.UseCors("AllowReactApp");

app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalErrorHandlingMiddleware>();

app.Use(async (context, next) =>
{
    var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = context.User?.FindFirst(ClaimTypes.Email)?.Value;

    if (!string.IsNullOrEmpty(userId))
    {
        using (Serilog.Context.LogContext.PushProperty("UserId", userId))
        {
            await next();
        }
        using (Serilog.Context.LogContext.PushProperty("emailId", email))
        {
            await next();
        }
    }
    else
    {
        await next();
    }
});

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        if (context.Database.CanConnect())
        {
            logger.LogInformation("✅ Database connection successful!");
        }
        else
        {
            logger.LogWarning("❌ Database connection failed!");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Error while connecting to the database.");
    }

    // services.GetRequiredService<ScalpingDeduction>();
}

var MT5Connection = app.Services.GetRequiredService<MT5Connection>();
app.Services.GetRequiredService<SymbolStore>();

var dealSubscribe = app.Services.GetRequiredService<DealSubscribe>();
MT5Connection.m_manager.DealSubscribe(dealSubscribe);

app.MapHub<ProfitOutDealHub>("/profitoutdealhub");

app.UseAuthorization();
app.MapControllers();

app.Run();


