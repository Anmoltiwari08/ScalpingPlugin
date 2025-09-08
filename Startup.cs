using Microsoft.EntityFrameworkCore;
using TestScalpingBackend.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ProfitOutDeals> ProfitOutDeals { get; set; }
    public DbSet<ScalpingSymbols> ScalpingSymbols { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProfitOutDeals>(entity =>
        {
            entity.Property(e => e.OpeningTime)
                  .HasColumnType("timestamp without time zone");

            entity.Property(e => e.ClosingTime)
                  .HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.CreatedAt)
                  .HasColumnType("timestamp without time zone")
                  .HasDefaultValueSql("NOW()"); 

                  
        });

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Email = "Support@fxsmartbull.com",
                PasswordHash = "$2y$10$nd4YLMsfFwrLJjVffrEi4uOURDHuZaqB6WCAkoj8oBtT46ROd4shK",
                

            }
        );
    }
}
