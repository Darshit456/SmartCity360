using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data
{
    public class SmartCityDbContext : DbContext
    {
        public SmartCityDbContext(DbContextOptions<SmartCityDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();

                // Use UTC time in database
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                // 🔥 DATABASE-LEVEL ROLE VALIDATION
                entity.Property(e => e.Role)
                    .HasDefaultValue("Citizen")
                    .HasMaxLength(50);

                // Add check constraint for valid roles
                entity.HasCheckConstraint("CK_User_Role", 
                    "\"Role\" IN ('Admin', 'CityPlanner', 'Citizen')");
            });
        }
    }
}