using Microsoft.EntityFrameworkCore;
using PHCIS.Contracts;
namespace PHCIS.Api.Data;

public static class DemoData
{
    // Explicit developer command; ordinary application startup never migrates or seeds.
    public static async Task SeedAsync(BookingDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        var demos = new[]
        {
            new Clinic { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "Riverside Community Clinic", Address = "12 River Road, Johannesburg (demonstration)" },
            new Clinic { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Oak Park Primary Care", Address = "48 Oak Avenue, Pretoria (demonstration)" }
        };
        foreach (var clinic in demos)
            if (!await db.Clinics.AnyAsync(c => c.Id == clinic.Id, ct)) db.Clinics.Add(clinic);
        await db.SaveChangesAsync(ct);
        var today = ClinicTime.Today(clock);
        foreach (var clinic in demos)
        {
            var existing = await db.TimeSlots.Where(s => s.ClinicId == clinic.Id).Select(s => s.StartUtc).ToListAsync(ct);
            var starts = existing.ToHashSet();
            for (var day = 0; day < 14; day++)
            {
                var date = today.AddDays(day);
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                for (var halfHour = 0; halfHour < 12; halfHour++)
                {
                    var start = ClinicTime.ToUtc(date, new TimeOnly(8, 0).AddMinutes(halfHour * 30));
                    if (start <= clock.GetUtcNow().UtcDateTime || starts.Contains(start)) continue;
                    db.TimeSlots.Add(new TimeSlot { ClinicId = clinic.Id, StartUtc = start, EndUtc = start.AddMinutes(30) });
                }
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
