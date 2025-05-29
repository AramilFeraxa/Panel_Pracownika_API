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
        public DbSet<DelegationDate> DelegationDates { get; set; }
        public DbSet<UserSalary> UserSalaries { get; set; }
        public DbSet<SalaryRecord> SalaryRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WorkTime>()
                .HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}
