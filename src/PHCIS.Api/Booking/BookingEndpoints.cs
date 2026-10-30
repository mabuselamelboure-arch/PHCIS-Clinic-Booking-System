using Microsoft.EntityFrameworkCore;
using PHCIS.Api.Data;
using PHCIS.Contracts;
namespace PHCIS.Api.Booking;

public static class BookingEndpoints
{
    public static void MapBookingEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/clinics", async (BookingDbContext db, CancellationToken ct) =>
            await db.Clinics.AsNoTracking().OrderBy(c => c.Name)
                .Select(c => new ClinicResponse(c.Id, c.Name, c.Address)).ToListAsync(ct));
        api.MapGet("/clinics/{clinicId:guid}/slots", async (Guid clinicId, DateOnly date,
            BookingDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var today = ClinicTime.Today(clock);
            if (date < today || date > today.AddDays(90))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Choose a date from today up to 90 days ahead."] });
            if (!await db.Clinics.AnyAsync(c => c.Id == clinicId, ct)) return Results.NotFound();
            var start = ClinicTime.ToUtc(date, TimeOnly.MinValue);
            var end = start.AddDays(1);
            var now = clock.GetUtcNow().UtcDateTime;
            var slots = await db.TimeSlots.AsNoTracking()
                .Where(s => s.ClinicId == clinicId && s.StartUtc >= start && s.StartUtc < end && s.StartUtc > now
                    && !db.Appointments.Any(a => a.TimeSlotId == s.Id))
                .OrderBy(s => s.StartUtc)
                .Select(s => new TimeSlotResponse(s.Id, s.ClinicId, s.StartUtc, s.EndUtc)).ToListAsync(ct);
            return Results.Ok(slots);
        });
        api.MapPost("/appointments", async (CreateBookingRequest request, IBookingService service, CancellationToken ct) =>
        {
            var result = await service.BookAsync(request, ct);
            return result.Status switch
            {
                BookingStatus.Created => Results.Json(result.Confirmation, statusCode: StatusCodes.Status201Created),
                BookingStatus.Invalid => Results.ValidationProblem(result.Errors!),
                BookingStatus.NotFound => Results.Problem(statusCode: 404, title: "Time slot not found."),
                BookingStatus.PastSlot => Results.Problem(statusCode: 400, title: "This time slot has already started. Choose another slot."),
                BookingStatus.Conflict => Results.Problem(statusCode: 409, title: "This slot was just booked. Choose another available slot."),
                _ => Results.Problem()
            };
        });
    }
}
