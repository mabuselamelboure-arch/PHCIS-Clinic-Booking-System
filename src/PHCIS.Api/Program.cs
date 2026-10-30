using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PHCIS.Api.Booking;
using PHCIS.Api.Data;
using PHCIS.Contracts;
using Serilog;
using PHCIS.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var localDemo = args.Contains("--demo-local", StringComparer.OrdinalIgnoreCase);
if (localDemo && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Local demo mode is only available in Development.");
builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration).WriteTo.Console());
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IValidator<CreateBookingRequest>, BookingRequestValidator>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddDbContext<BookingDbContext>(options =>
{
    if (localDemo) options.UseSqlite($"Data Source={builder.Configuration["DemoDatabasePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "clinic-demo-auth.db")}");
    else options.UseSqlServer(builder.Configuration.GetConnectionString("ClinicBooking")
        ?? throw new InvalidOperationException("Configure ConnectionStrings__ClinicBooking, or use --demo-local in Development."));
});
builder.AddPatientAuthentication();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("accounts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (localDemo)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DemoData.SeedAsync(db, TimeProvider.System);
}
if (args.Contains("--seed-demo", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    await DemoData.SeedAsync(scope.ServiceProvider.GetRequiredService<BookingDbContext>(), TimeProvider.System);
    return;
}
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
if (app.Environment.IsDevelopment()) { app.UseWebAssemblyDebugging(); app.MapOpenApi(); }
else { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapStaticAssets();
app.MapPatientAuthentication();
app.MapBookingEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();
public partial class Program { }
