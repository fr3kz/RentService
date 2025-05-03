using RentService.Models.VehicleModule;
using Microsoft.EntityFrameworkCore;

namespace RentService.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Vehicle> Cars { get; set; }
    

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Walidacja: unikalny numer rejestracyjny
        modelBuilder.Entity<Vehicle>()
            .HasIndex(c => c.RegistrationNumber)
            .IsUnique();
    }
}



