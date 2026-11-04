using AuditTrails;
using Decisions;
using Flags;
using Microsoft.EntityFrameworkCore;
using Tickets;
using VendorResponses;

namespace Data
{
    public class ReviewDbContext : DbContext
    {
        public ReviewDbContext(DbContextOptions<ReviewDbContext> options) : base(options) { }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Flag> Flags { get; set; }
        public DbSet<VendorResponse> VendorResponses { get; set; }
        public DbSet<Decision> Decisions { get; set; }
        public DbSet<AuditTrail> AuditTrails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReviewDbContext).Assembly);
        }

        private void CheckImmutableRecords()
        {
            ChangeTracker.DetectChanges();
            if (ChangeTracker.Entries().Any(e => (e.Entity is Decision || e.Entity is AuditTrail)
                && (e.State == EntityState.Modified || e.State == EntityState.Deleted)))
            {
                throw new InvalidOperationException("Decisions and audit records cannot be changed or deleted.");
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            CheckImmutableRecords();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            CheckImmutableRecords();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
