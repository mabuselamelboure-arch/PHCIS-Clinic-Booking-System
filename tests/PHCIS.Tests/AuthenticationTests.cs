using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PHCIS.Api.Data;
using PHCIS.Api.Security;
using PHCIS.Contracts;
namespace PHCIS.Tests;

public sealed class AuthenticationTests : IDisposable
{
    private readonly BookingApplication app = new(realAuth: true);
    private readonly HttpClient client;
    private readonly Guid slotId = Guid.NewGuid();
    public AuthenticationTests()
    {
        client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.Database.EnsureCreated();
        db.TimeSlots.Add(new TimeSlot { Id = slotId, Clinic = new Clinic { Name = "Owned Clinic", Address = "Fictional" },
            StartUtc = BookingApplication.Now.UtcDateTime.AddHours(3), EndUtc = BookingApplication.Now.UtcDateTime.AddHours(4) });
        db.SaveChanges();
    }
    [Fact]
    public async Task Anonymous_booking_and_confirmation_are_rejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/appointments", ValidationTests.Valid(slotId))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/appointments/unknown")).StatusCode);
    }
    [Fact]
    public async Task Registration_login_and_owned_confirmation_work_without_exposing_other_users()
    {
        var email = $"patient-{Guid.NewGuid():N}@example.test";
        var password = "Aa!1" + Guid.NewGuid().ToString("N");
        var registration = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var auth = (await registration.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var booking = await client.PostAsJsonAsync("/api/appointments", ValidationTests.Valid(slotId));
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode);
        var reference = (await booking.Content.ReadFromJsonAsync<BookingConfirmation>())!.Reference;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/appointments/{reference}")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var second = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest($"other-{Guid.NewGuid():N}@example.test", password));
        var other = (await second.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/appointments/{reference}")).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var patient = await db.Patients.SingleAsync();
        var owner = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(owner.Id, patient.UserId);
    }
    [Fact]
    public async Task Tampered_expired_and_wrong_audience_tokens_are_rejected()
    {
        var settings = app.Services.GetRequiredService<TokenSettings>();
        foreach (var scenario in new[] { "expired", "audience", "signature" })
        {
            var key = scenario == "signature" ? System.Security.Cryptography.RandomNumberGenerator.GetBytes(64) : settings.Key;
            var jwt = new JwtSecurityToken(settings.Issuer, scenario == "audience" ? "wrong" : settings.Audience,
                [new Claim(JwtRegisteredClaimNames.Sub, "untrusted")], DateTime.UtcNow.AddHours(-1),
                scenario == "expired" ? DateTime.UtcNow.AddMinutes(-30) : DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/appointments/unknown")).StatusCode);
        }
    }
    [Fact]
    public async Task Weak_password_invalid_email_and_duplicate_account_are_rejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("bad", "short"))).StatusCode);
        var email = $"patient-{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "alllowercasepassword"))).StatusCode);
        var account = new RegisterRequest(email, "Aa!1" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register", account)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", account)).StatusCode);
    }
    [Fact]
    public async Task Repeated_bad_passwords_lock_account()
    {
        var account = new RegisterRequest($"patient-{Guid.NewGuid():N}@example.test", "Aa!1" + Guid.NewGuid().ToString("N"));
        await client.PostAsJsonAsync("/api/auth/register", account);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(account.Email, "Wrong!12345"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(account.Email, account.Password))).StatusCode);
    }
    [Fact]
    public async Task Mismatched_clinic_slot_is_rejected()
    {
        var registered = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest($"patient-{Guid.NewGuid():N}@example.test", "Aa!1" + Guid.NewGuid().ToString("N")));
        var auth = (await registered.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var request = ValidationTests.Valid(slotId); request.ClinicId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/appointments", request)).StatusCode);
    }
    public void Dispose() { client.Dispose(); app.Dispose(); }
}
