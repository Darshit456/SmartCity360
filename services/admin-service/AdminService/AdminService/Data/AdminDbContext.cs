using Microsoft.EntityFrameworkCore;

namespace AdminService.Models
{
    public class AdminDbContext : DbContext
    {
        public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
        {
        }

        public DbSet<AdminLog> AdminLogs { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<DashboardWidget> DashboardWidgets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure AdminLog
            modelBuilder.Entity<AdminLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Details).HasMaxLength(1000);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.Timestamp).HasDefaultValueSql("NOW()");
            });

            // Configure SystemSetting
            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.Value).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Description).HasMaxLength(200);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
            });

            // Configure DashboardWidget
            modelBuilder.Entity<DashboardWidget>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.WidgetType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Configuration).HasMaxLength(2000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            });
        }
    }
}