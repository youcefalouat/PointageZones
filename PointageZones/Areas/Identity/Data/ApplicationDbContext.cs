using System.Data;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PointageZones.Areas.Identity.Data;
using PointageZones.Models;

namespace PointageZones.Data;

public class ApplicationDbContext : IdentityDbContext<User>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    { }
        public DbSet<Zone> Zones { get; set; }
        public DbSet<Tour> Tours { get; set; }
        public DbSet<PlanTour> PlanTours { get; set; }
        public DbSet<PointageAgent> Pointages { get; set; }
        public DbSet<Observation> Observations { get; set; } 
        public DbSet<PushSubscription> PushSubscriptions { get; set; }


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Customize the ASP.NET Identity model and override the defaults if needed.
        // For example, you can rename the ASP.NET Identity table names and more.
        // Add your customizations after calling base.OnModelCreating(builder);

        // Configuration Many-to-Many entre Tours et Zones
            builder.Entity<PlanTour>()
            .HasOne(pt => pt.Tour)
            .WithMany(t => t.PlanTours)
            .HasForeignKey(pt => pt.TourId);

            builder.Entity<PlanTour>()
            .HasOne(pt => pt.Zone)
            .WithMany(z => z.PlanTours)
            .HasForeignKey(pt => pt.ZoneId);

        // Configuration One-to-Many entre PointageAgent et autres tables
            builder.Entity<PointageAgent>()
            .HasOne(pz => pz.User)
            .WithMany(u => u.Pointages)
            .HasForeignKey(pz => pz.UserId);

            builder.Entity<PointageAgent>()
            .HasOne(pz => pz.PlanTour)
            .WithMany(t => t.Pointages)
            .HasForeignKey(pz => pz.PlanTourId);

            builder.Entity<PointageAgent>()
                      .HasOne(e => e.Observation)
                      .WithMany(o => o.PointageAgents)
                      .HasForeignKey(e => e.ObservationId)
                      .IsRequired(false) // Optional relationship
                      .OnDelete(DeleteBehavior.SetNull); // Set null if observation is deleted
                
            

    }
}
