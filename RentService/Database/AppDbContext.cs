using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using RentService.Models.VehicleModule;
using Microsoft.EntityFrameworkCore;
using RentService.Models;
using RentService.Area.EmployeModule.Models;

namespace RentService.Database;

public class AppDbContext : IdentityDbContext<User, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Vehicle> Cars { get; set; }
    public DbSet<Employee> Employees { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Konfiguracja Vehicle
        modelBuilder.Entity<Vehicle>()
            .HasIndex(c => c.RegistrationNumber)
            .IsUnique();

        // Konfiguracja User
        modelBuilder.Entity<User>()
            .HasIndex(c => c.Email)
            .IsUnique();

        // Konfiguracja Employee
        modelBuilder.Entity<Employee>(entity =>
        {
            // Klucz główny
            entity.HasKey(e => e.EmployeeId);

            

            // Konfiguracja właściwości
            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(100);

          
            entity.Property(e => e.Gender)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.Street)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.PostalCode)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.City)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Country)
                .HasMaxLength(100)
                .HasDefaultValue("Polska");

            entity.Property(e => e.Nationality)
                .HasMaxLength(100)
                .HasDefaultValue("Polskie");

            entity.Property(e => e.ContractType)
                .IsRequired()
                .HasMaxLength(100);

         

            entity.Property(e => e.PersonalPhone)
                .HasMaxLength(20);

            entity.Property(e => e.BirthPlace)
                .HasMaxLength(100);

            entity.Property(e => e.Education)
                .HasMaxLength(100);

            entity.Property(e => e.ImagePath)
                .HasMaxLength(500);

            entity.Property(e => e.ContractPath)
                .HasMaxLength(500);

            entity.Property(e => e.IdCopyPath)
                .HasMaxLength(500);

            entity.Property(e => e.OtherDocumentsPath)
                .HasMaxLength(2000);

            entity.Property(e => e.Notes)
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100);

            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100);
          

           

           
        });

       
    }
}