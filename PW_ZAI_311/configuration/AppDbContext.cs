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
        public DbSet<User> Users { get; init; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
            // Wyłączenie transakcji dla instancji MongoDB Standalone
            Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Sensor
            modelBuilder.Entity<Sensor>(entity =>
            {
                entity.ToCollection("Sensors");
                entity.HasKey(x => x.Id);
                // Ignorujemy właściwość nawigacyjną i alias, aby EF MongoDB nie zagnieżdżał dokumentu serii
                entity.Ignore(x => x.MeasurementSerie);
                entity.Ignore(x => x.Name);
                entity.HasIndex(x => x.ApiKeyHash);
                entity.HasIndex(x => x.MeasurementSerieId);
            });

            // 2. Measurement
            modelBuilder.Entity<Measurement>(entity =>
            {
                entity.ToCollection("Measurements");
                entity.HasKey(x => x.Id);
                // Ignorujemy właściwości nawigacyjne (relacja odbywa się po SensorId i MeasurementSerieId)
                entity.Ignore(x => x.Sensor);
                entity.Ignore(x => x.MeasurementSerie);
                // Indeksy dla szybkiego filtrowania po czasie i serii (Wymaganie T4 / F5)
                entity.HasIndex(x => x.DateTime);
                entity.HasIndex(x => x.MeasurementSerieId);
                entity.HasIndex(x => x.SensorId);
            });

            // 3. MeasurementSerie
            modelBuilder.Entity<MeasurementSerie>(entity =>
            {
                entity.ToCollection("MeasurementSeries");
                entity.HasKey(x => x.Id);
            });

            // 4. User
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToCollection("Users");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Username);
            });

            // 5. Log
            modelBuilder.Entity<Log>(entity =>
            {
                entity.ToCollection("Logs");
                entity.HasKey(x => x.Id);
            });
        }
    }
}