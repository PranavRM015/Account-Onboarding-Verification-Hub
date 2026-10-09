using Microsoft.EntityFrameworkCore;
using AuditTrails;
using Customers;
using Decisions;
using Flags;
using Tickets;
using VendorResponses;
namespace Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<AuditTrail> AuditTrails { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Decision> Decisions { get; set; }
        public DbSet<Flag> Flags { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<VendorResponse> VendorResponses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
