using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PHCIS.Api.Data;
using PHCIS.Contracts;
namespace PHCIS.Tests;

public sealed class BookingApiTests : IDisposable
{
    private readonly BookingApplication app = new();
    private readonly HttpClient client;
    public BookingApiTests()
    {
        client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.Database.EnsureCreated();
        db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser { Id = "test-patient", UserName = "test@example.test" });
        db.TimeSlots.AddRange(
            new TimeSlot { Id = BookingApplication.AvailableId, Clinic = new Clinic { Id = BookingApplication.ClinicId, Name = "Test Clinic", Address = "Fictional address" }, StartUtc = BookingApplication.Now.UtcDateTime.AddHours(3), EndUtc = BookingApplication.Now.UtcDateTime.AddHours(3.5) },
            new TimeSlot { Id = BookingApplication.ExpiredId, ClinicId = BookingApplication.ClinicId, StartUtc = BookingApplication.Now.UtcDateTime.AddHours(-1), EndUtc = BookingApplication.Now.UtcDateTime.AddMinutes(-30) });
        db.SaveChanges();
    }
    [Fact]
    public async Task Clinics_are_listed()
    {
        var clinics = await client.GetFromJsonAsync<List<ClinicResponse>>("/api/clinics");
        Assert.Equal("Test Clinic", Assert.Single(clinics!).Name);
    }
    [Fact]
    public async Task Availability_excludes_expired_and_booked_slots()
    {
        var slots = await client.GetFromJsonAsync<List<TimeSlotResponse>>(SlotsUrl());
        Assert.Equal(BookingApplication.AvailableId, Assert.Single(slots!).Id);
        var response = await client.PostAsJsonAsync("/api/appointments", ValidationTests.Valid(BookingApplication.AvailableId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<TimeSlotResponse>>(SlotsUrl()))!);
    }
    [Fact]
    public async Task Successful_booking_is_persisted_with_confirmation()
    {
        var request = ValidationTests.Valid(BookingApplication.AvailableId);
        request.FullName = "  Alex Example  ";
        var response = await client.PostAsJsonAsync("/api/appointments", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var confirmation = await response.Content.ReadFromJsonAsync<BookingConfirmation>();
        Assert.StartsWith("PHC-", confirmation!.Reference);
        Assert.Equal("Test Clinic", confirmation.ClinicName);
        Assert.Equal(DateTimeKind.Utc, confirmation.StartUtc.Kind);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        Assert.Equal(1, await db.Appointments.CountAsync());
        Assert.Equal("Alex Example", (await db.Patients.SingleAsync()).FullName);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("alex@example.test", json);
        Assert.DoesNotContain("fullName", json);
    }
    [Fact]
    public async Task Repeated_booking_returns_conflict_without_orphan_patient()
    {
        var request = ValidationTests.Valid(BookingApplication.AvailableId);
        await client.PostAsJsonAsync("/api/appointments", request);
        var response = await client.PostAsJsonAsync("/api/appointments", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        Assert.Equal(1, await db.Appointments.CountAsync());
        Assert.Equal(1, await db.Patients.CountAsync());
    }
    [Fact]
    public async Task Invalid_patient_fields_return_validation_problem()
    {
        var response = await client.PostAsJsonAsync("/api/appointments", new CreateBookingRequest());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("errors", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Unknown_slot_returns_not_found()
    {
        var response = await client.PostAsJsonAsync("/api/appointments", ValidationTests.Valid());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task Expired_slot_cannot_be_booked()
    {
        var response = await client.PostAsJsonAsync("/api/appointments", ValidationTests.Valid(BookingApplication.ExpiredId));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Theory]
    [InlineData("2029-12-01")]
    [InlineData("2031-01-01")]
    [InlineData("not-a-date")]
    public async Task Invalid_availability_date_returns_bad_request(string date)
    {
        var response = await client.GetAsync($"/api/clinics/{BookingApplication.ClinicId}/slots?date={date}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Unknown_clinic_returns_not_found()
    {
        var response = await client.GetAsync($"/api/clinics/{Guid.NewGuid()}/slots?date=2030-01-07");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task Unknown_api_route_does_not_return_spa_html()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/missing")).StatusCode);
    }
    [Fact]
    public async Task Database_enforces_one_appointment_per_slot_atomically()
    {
        await client.PostAsJsonAsync("/api/appointments", ValidationTests.Valid(BookingApplication.AvailableId));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.Appointments.Add(new Appointment { TimeSlotId = BookingApplication.AvailableId,
            Patient = new Patient { FullName = "Second Example", Email = "second@example.test", Phone = "0820000000" },
            Reference = $"PHC-{Guid.NewGuid():N}", CreatedUtc = BookingApplication.Now.UtcDateTime });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Appointments.CountAsync());
        Assert.Equal(1, await db.Patients.CountAsync());
    }
    [Fact]
    public async Task Demo_seed_can_be_repeated_without_duplicating_slots()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        await DemoData.SeedAsync(db, clock);
        var count = await db.TimeSlots.CountAsync();
        await DemoData.SeedAsync(db, clock);
        Assert.Equal(count, await db.TimeSlots.CountAsync());
    }
    [Fact]
    public async Task Availability_uses_SAST_day_boundaries_and_returns_UTC_timestamps()
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            db.TimeSlots.AddRange(
                new TimeSlot { ClinicId = BookingApplication.ClinicId, StartUtc = new DateTime(2030, 1, 7, 22, 30, 0, DateTimeKind.Utc), EndUtc = new DateTime(2030, 1, 7, 23, 0, 0, DateTimeKind.Utc) },
                new TimeSlot { ClinicId = BookingApplication.ClinicId, StartUtc = new DateTime(2030, 1, 8, 22, 0, 0, DateTimeKind.Utc), EndUtc = new DateTime(2030, 1, 8, 22, 30, 0, DateTimeKind.Utc) });
            await db.SaveChangesAsync();
        }
        var slots = await client.GetFromJsonAsync<List<TimeSlotResponse>>($"/api/clinics/{BookingApplication.ClinicId}/slots?date=2030-01-08");
        var slot = Assert.Single(slots!);
        Assert.Equal(new DateTime(2030, 1, 7, 22, 30, 0, DateTimeKind.Utc), slot.StartUtc);
        Assert.Equal(DateTimeKind.Utc, slot.StartUtc.Kind);
    }
    [Fact]
    public async Task Hosted_client_and_component_library_assets_are_served()
    {
        using var hosted = new BookingApplication(staticAssets: true);
        using var browser = hosted.CreateClient();
        var home = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("id=\"app\"", await home.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/_content/MudBlazor/MudBlazor.min.css")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/_content/MudBlazor/MudBlazor.min.js")).StatusCode);
    }
    private static string SlotsUrl() => $"/api/clinics/{BookingApplication.ClinicId}/slots?date=2030-01-07";
    public void Dispose() { client.Dispose(); app.Dispose(); }
}

internal sealed class BookingApplication : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Now = new(2030, 1, 7, 5, 0, 0, TimeSpan.Zero);
    public static readonly Guid ClinicId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid AvailableId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid ExpiredId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly bool staticAssets;
    private readonly bool realAuth;
    public BookingApplication(bool staticAssets = false, bool realAuth = false) { this.staticAssets = staticAssets; this.realAuth = realAuth; connection.Open(); }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(staticAssets ? "Development" : "Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Serilog:MinimumLevel:Default"] = "Fatal" }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BookingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BookingDbContext>>();
            services.AddDbContext<BookingDbContext>(options => options.UseSqlite(connection));
            if (!realAuth) TestAuthentication.Add(services);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock(Now));
        });
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}
