using Microsoft.EntityFrameworkCore;
using TestScalpingBackend.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<ProfitOutDeals> ProfitOutDeals { get; set; }
    public DbSet<ScalpingSymbols> ScalpingSymbols { get; set; }

}


