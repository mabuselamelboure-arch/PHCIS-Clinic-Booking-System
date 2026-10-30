using Microsoft.EntityFrameworkCore;
namespace PHCIS.Api.Data;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Patient>(e =>
        {
            e.Property(p => p.FullName).HasMaxLength(120).IsRequired();
            e.Property(p => p.Email).HasMaxLength(254).IsRequired();
            e.Property(p => p.Phone).HasMaxLength(25).IsRequired();
        });
        model.Entity<Clinic>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(120).IsRequired();
            e.Property(c => c.Address).HasMaxLength(250).IsRequired();
        });
        model.Entity<TimeSlot>(e =>
        {
            e.Property(s => s.StartUtc).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            e.Property(s => s.EndUtc).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            e.HasIndex(s => new { s.ClinicId, s.StartUtc }).IsUnique();
            e.HasOne(s => s.Clinic).WithMany().HasForeignKey(s => s.ClinicId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_TimeSlots_EndAfterStart", "[EndUtc] > [StartUtc]"));
        });
        model.Entity<Appointment>(e =>
        {
            // Final protection when concurrent requests select the same slot.
            e.Property(a => a.CreatedUtc).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            e.HasIndex(a => a.TimeSlotId).IsUnique();
            e.HasIndex(a => a.Reference).IsUnique();
            e.Property(a => a.Reference).HasMaxLength(36).IsRequired();
            e.HasOne(a => a.Patient).WithMany().HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.TimeSlot).WithMany().HasForeignKey(a => a.TimeSlotId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
