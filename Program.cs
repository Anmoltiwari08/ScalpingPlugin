using TestScalpingBackend.Services;
using Microsoft.EntityFrameworkCore;
      
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5065);
});

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<DealSubscribe>();
builder.Services.AddScoped<MT5Operations>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy.WithOrigins(
                        //   "http://194.35.120.26:3002"
                         "http://localhost:3002"
                                     )
                        .AllowCredentials()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();
app.UseCors("AllowReactApp");

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
}

app.Services.GetRequiredService<DealSubscribe>();
app.MapHub<ProfitOutDealHub>("/profitoutdealhub");

app.UseAuthorization();
app.MapControllers();

app.Run();


