using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PHCIS.Api.Booking;
using PHCIS.Api.Data;
using PHCIS.Contracts;
namespace PHCIS.Tests;

public sealed class DomainFailureTests
{
    [Fact]
    public async Task Unexpected_foreign_key_failure_is_not_reported_as_booking_conflict()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BookingDbContext>().UseSqlite(connection)
            .AddInterceptors(new BrokenForeignKey()).Options;
        await using var db = new BookingDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var slot = new TimeSlot { Clinic = new Clinic { Name = "Failure test", Address = "Fictional" },
            StartUtc = BookingApplication.Now.UtcDateTime.AddHours(1), EndUtc = BookingApplication.Now.UtcDateTime.AddHours(2) };
        db.TimeSlots.Add(slot); await db.SaveChangesAsync();
        var service = new BookingService(db, new BookingRequestValidator(), new FixedClock(BookingApplication.Now), NullLogger<BookingService>.Instance);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => service.BookAsync(ValidationTests.Valid(slot.Id)));
        Assert.True(exception.InnerException is SqliteException { SqliteExtendedErrorCode: 787 });
        db.ChangeTracker.Clear();
        Assert.Equal(0, await db.Appointments.CountAsync());
        Assert.Equal(0, await db.Patients.CountAsync());
    }
    private sealed class BrokenForeignKey : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var appointment = data.Context!.ChangeTracker.Entries<Appointment>().SingleOrDefault(e => e.State == EntityState.Added)?.Entity;
            if (appointment is not null)
            {
                appointment.Patient.UserId = "non-existent-identity";
            }
            return ValueTask.FromResult(result);
        }
    }
}
