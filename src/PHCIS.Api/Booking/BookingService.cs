using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PHCIS.Api.Data;
using PHCIS.Contracts;
namespace PHCIS.Api.Booking;

public enum BookingStatus { Created, Invalid, NotFound, PastSlot, Conflict }
public sealed record BookingResult(BookingStatus Status, BookingConfirmation? Confirmation = null,
    IDictionary<string, string[]>? Errors = null);
public interface IBookingService
{
    Task<BookingResult> BookAsync(CreateBookingRequest request, CancellationToken ct = default, string? ownerId = null);
}
public sealed class BookingService(BookingDbContext db, IValidator<CreateBookingRequest> validator,
    TimeProvider clock, ILogger<BookingService> logger) : IBookingService
{
    public async Task<BookingResult> BookAsync(CreateBookingRequest request, CancellationToken ct = default, string? ownerId = null)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return new(BookingStatus.Invalid, Errors: validation.ToDictionary());
        var slot = await db.TimeSlots.Include(s => s.Clinic).SingleOrDefaultAsync(s => s.Id == request.TimeSlotId, ct);
        if (slot is null) return new(BookingStatus.NotFound);
        if (request.ClinicId is { } clinicId && clinicId != slot.ClinicId)
            return new(BookingStatus.Invalid, Errors: new Dictionary<string, string[]> { ["ClinicId"] = ["The selected slot does not belong to this clinic."] });
        if (slot.StartUtc <= clock.GetUtcNow().UtcDateTime) return new(BookingStatus.PastSlot);
        if (await db.Appointments.AnyAsync(a => a.TimeSlotId == slot.Id, ct)) return new(BookingStatus.Conflict);
        // SaveChanges uses one transaction for both inserts, preventing orphan patients on conflict.
        var appointment = new Appointment
        {
            Patient = new Patient { UserId = ownerId, FullName = request.FullName.Trim(), Email = request.Email.Trim(), Phone = request.Phone.Trim() },
            TimeSlotId = slot.Id,
            Reference = $"PHC-{Guid.NewGuid():N}",
            CreatedUtc = clock.GetUtcNow().UtcDateTime
        };
        db.Appointments.Add(appointment);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } or SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            db.ChangeTracker.Clear();
            if (!await db.Appointments.AnyAsync(a => a.TimeSlotId == slot.Id, ct)) throw;
            logger.LogInformation("Booking conflict for slot {SlotId}", slot.Id);
            return new(BookingStatus.Conflict);
        }
        logger.LogInformation("Appointment {AppointmentId} created for slot {SlotId}", appointment.Id, slot.Id);
        return new(BookingStatus.Created, new BookingConfirmation(appointment.Reference, slot.Clinic.Name,
            slot.Clinic.Address, slot.StartUtc, slot.EndUtc));
    }
}
