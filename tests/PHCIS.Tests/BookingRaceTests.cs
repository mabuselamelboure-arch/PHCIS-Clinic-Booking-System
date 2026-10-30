using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PHCIS.Api.Booking;
using PHCIS.Api.Data;
using PHCIS.Contracts;
namespace PHCIS.Tests;

public sealed class BookingRaceTests
{
    [Fact]
    public async Task Another_request_winning_after_availability_check_returns_conflict()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var plainOptions = new DbContextOptionsBuilder<BookingDbContext>().UseSqlite(connection).Options;
        var clock = new FixedClock(BookingApplication.Now);
        await using (var setup = new BookingDbContext(plainOptions))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.TimeSlots.Add(new TimeSlot { Id = BookingApplication.AvailableId,
                Clinic = new Clinic { Name = "Race Test", Address = "Fictional" },
                StartUtc = BookingApplication.Now.UtcDateTime.AddHours(1), EndUtc = BookingApplication.Now.UtcDateTime.AddHours(2) });
            await setup.SaveChangesAsync();
        }
        // Deterministically commit a competing booking after the service's AnyAsync check,
        // immediately before the losing request attempts its insert.
        var interceptor = new CompetingBookingInterceptor(async () =>
        {
            await using var winner = new BookingDbContext(plainOptions);
            winner.Appointments.Add(new Appointment { TimeSlotId = BookingApplication.AvailableId,
                Patient = new Patient { FullName = "Winner Example", Email = "winner@example.test", Phone = "0820000000" },
                Reference = $"PHC-{Guid.NewGuid():N}", CreatedUtc = BookingApplication.Now.UtcDateTime });
            await winner.SaveChangesAsync();
        });
        var options = new DbContextOptionsBuilder<BookingDbContext>().UseSqlite(connection).AddInterceptors(interceptor).Options;
        await using var loser = new BookingDbContext(options);
        var service = new BookingService(loser, new BookingRequestValidator(), clock, NullLogger<BookingService>.Instance);
        var result = await service.BookAsync(ValidationTests.Valid(BookingApplication.AvailableId));
        Assert.Equal(BookingStatus.Conflict, result.Status);
        Assert.Equal(1, await loser.Appointments.CountAsync());
        Assert.Equal(1, await loser.Patients.CountAsync());
    }
    private sealed class CompetingBookingInterceptor(Func<Task> action) : SaveChangesInterceptor
    {
        private bool invoked;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!invoked) { invoked = true; await action(); }
            return result;
        }
    }
}
