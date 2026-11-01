using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;
using PW_ZAI_311.Entieties;
namespace PW_ZAI_311.configuration
{

    public class AppDbContext : DbContext
    {
        public DbSet<Sensor> Products { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Sensor>().ToCollection("products");
        }
    }
}