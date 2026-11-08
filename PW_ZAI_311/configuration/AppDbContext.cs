using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;
using PW_ZAI_311.Entieties;
namespace PW_ZAI_311.configuration
{

    public class AppDbContext : DbContext
    {
        public DbSet<Sensor> Sensors { get; set; }
        public DbSet<Measurement> Measurements { get; set; }
        public DbSet<MeasurementSerie> MeasurementSeries { get; set; }
        public DbSet<Log> Logs { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Sensor>().ToCollection("Sensors");
            modelBuilder.Entity<Measurement>().ToCollection("Measurements");
            modelBuilder.Entity<MeasurementSerie>().ToCollection("MeasurementSeries");
            modelBuilder.Entity<Log>().ToCollection("Logs");

        }
    }
}