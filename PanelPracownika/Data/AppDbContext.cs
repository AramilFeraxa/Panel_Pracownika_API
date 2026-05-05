using Microsoft.EntityFrameworkCore;
using PanelPracownika.Models;

namespace PanelPracownika.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<UserTask> UserTasks { get; set; }
        public DbSet<WorkTime> WorkTimes { get; set; }
        public DbSet<Login> Users { get; set; }
        public DbSet<AbsenceDate> AbsenceDates { get; set; }
        public DbSet<UserSalary> UserSalaries { get; set; }
        public DbSet<SalaryRecord> SalaryRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Login>()
                .Property(u => u.Email)
                .IsRequired(false)
                .HasDefaultValue("");

            modelBuilder.Entity<AbsenceDate>()
                .Property(a => a.Type)
                .IsRequired(false)
                .HasDefaultValue("Wyjazd");

            modelBuilder.Entity<AbsenceDate>()
                .Property(a => a.Reason)
                .IsRequired(false)
                .HasDefaultValue("Wyjazd");

            modelBuilder.Entity<WorkTime>()
                .HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserTask>()
                .HasIndex(t => t.UserId);

            modelBuilder.Entity<UserSalary>()
                .HasIndex(s => s.UserId);

            modelBuilder.Entity<SalaryRecord>()
                .HasIndex(s => s.UserId);

            modelBuilder.Entity<AbsenceDate>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}
