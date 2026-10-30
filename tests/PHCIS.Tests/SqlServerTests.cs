using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PHCIS.Api.Data;
using PHCIS.Contracts;
namespace PHCIS.Tests;

// Opt-in infrastructure tests. Each test owns a newly named database and never targets caller data.
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHCIS_SQLSERVER")))
            Skip = "Set PHCIS_SQLSERVER to run against an isolated SQL Server assessment database.";
    }
}

public sealed class SqlServerTests
{
    [SqlServerFact]
    public async Task Migrations_relationships_and_real_concurrent_HTTP_bookings_are_correct()
    {
        await using var app = new SqlServerApplication();
        using var client = app.CreateClient();
        await app.PrepareAsync();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.True(await db.Database.CanConnectAsync());
            await DemoData.SeedAsync(db, new FixedClock(BookingApplication.Now));
            Assert.Equal(3, await db.Clinics.CountAsync());
            Assert.True(await db.TimeSlots.AnyAsync());
        }
        var request = ValidationTests.Valid(SqlServerApplication.SlotId);
        // Gate both independent HTTP requests so they are dispatched concurrently, not sequentially.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Send()
        {
            await gate.Task;
            return await client.PostAsJsonAsync("/api/appointments", request);
        }
        var first = Task.Run(Send); var second = Task.Run(Send);
        gate.SetResult();
        var responses = await Task.WhenAll(first, second);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var success = responses.Single(r => r.StatusCode == HttpStatusCode.Created);
        var confirmation = await success.Content.ReadFromJsonAsync<BookingConfirmation>();
        Assert.NotNull(confirmation);
        Assert.Equal(DateTimeKind.Utc, confirmation.StartUtc.Kind);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/appointments/{confirmation.Reference}")).StatusCode);
        var slots = await client.GetFromJsonAsync<List<TimeSlotResponse>>($"/api/clinics/{SqlServerApplication.ClinicId}/slots?date=2030-01-07");
        Assert.DoesNotContain(slots!, s => s.Id == SqlServerApplication.SlotId);
        using var checkScope = app.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<BookingDbContext>();
        Assert.Equal(1, await check.Appointments.CountAsync());
        Assert.Equal(1, await check.Patients.CountAsync());
        // Force the database unique index to reject a transaction with a second patient insert.
        check.Appointments.Add(new Appointment { TimeSlotId = SqlServerApplication.SlotId,
            Reference = $"PHC-{Guid.NewGuid():N}", CreatedUtc = BookingApplication.Now.UtcDateTime,
            Patient = new Patient { FullName = "Rejected Example", Email = "rejected@example.test", Phone = "0820000000" } });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => check.SaveChangesAsync());
        Assert.True(ex.InnerException is SqlException { Number: 2601 or 2627 });
        check.ChangeTracker.Clear();
        Assert.Equal(1, await check.Patients.CountAsync());
        Assert.Equal(1, await check.Appointments.CountAsync());
        // Foreign keys also execute on SQL Server, not just in the EF model.
        check.Appointments.Add(new Appointment { PatientId = Guid.NewGuid(), TimeSlotId = Guid.NewGuid(),
            Reference = $"PHC-{Guid.NewGuid():N}", CreatedUtc = BookingApplication.Now.UtcDateTime });
        var fk = await Assert.ThrowsAsync<DbUpdateException>(() => check.SaveChangesAsync());
        Assert.True(fk.InnerException is SqlException { Number: 547 });
        foreach (var response in responses) response.Dispose();
    }
}

internal sealed class SqlServerApplication : WebApplicationFactory<Program>
{
    internal static readonly Guid ClinicId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    internal static readonly Guid SlotId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private readonly string connection;
    public SqlServerApplication()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PHCIS_SQLSERVER"));
        builder.InitialCatalog = $"PHCIS_Assessment_Test_{Guid.NewGuid():N}";
        connection = builder.ConnectionString;
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Serilog:MinimumLevel:Default"] = "Fatal" }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BookingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BookingDbContext>>();
            var gate = new ConcurrentSaveGate();
            services.AddDbContext<BookingDbContext>(o => o.UseSqlServer(connection).AddInterceptors(gate));
            TestAuthentication.Add(services);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock(BookingApplication.Now));
        });
    }
    internal async Task PrepareAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await db.Database.MigrateAsync();
        db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser { Id = "test-patient", UserName = "test@example.test" });
        db.TimeSlots.Add(new TimeSlot { Id = SlotId, Clinic = new Clinic { Id = ClinicId, Name = "SQL Verification Clinic", Address = "Fictional" },
            StartUtc = BookingApplication.Now.UtcDateTime.AddHours(3), EndUtc = BookingApplication.Now.UtcDateTime.AddHours(3.5) });
        await db.SaveChangesAsync();
    }
    public override async ValueTask DisposeAsync()
    {
        // Only the unique database generated by this fixture can be deleted.
        var catalog = new SqlConnectionStringBuilder(connection).InitialCatalog;
        if (!catalog.StartsWith("PHCIS_Assessment_Test_", StringComparison.Ordinal) || catalog.Length != 54)
            throw new InvalidOperationException("Refusing cleanup of a database not owned by this fixture.");
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<BookingDbContext>().Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
}

internal sealed class ConcurrentSaveGate : SaveChangesInterceptor
{
    private int arrivals;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<Appointment>().Any(e => e.State == EntityState.Added))
        {
            var count = Interlocked.Increment(ref arrivals);
            if (count == 2) ready.TrySetResult();
            if (count <= 2) await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        return result;
    }
}
