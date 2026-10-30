using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PHCIS.Api.Booking;
using PHCIS.Api.Data;
using PHCIS.Contracts;
using Serilog;

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
    if (localDemo) options.UseSqlite($"Data Source={Path.Combine(builder.Environment.ContentRootPath, "clinic-demo.db")}");
    else options.UseSqlServer(builder.Configuration.GetConnectionString("ClinicBooking")
        ?? throw new InvalidOperationException("Configure ConnectionStrings__ClinicBooking, or use --demo-local in Development."));
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
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapBookingEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();
public partial class Program { }
