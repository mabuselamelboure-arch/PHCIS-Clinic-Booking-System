using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace PHCIS.Api.Data;

/// <summary>Generate SQL Server migrations without connecting to a database or reading credentials.</summary>
public sealed class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>();
        // Migrations need the provider. Only database-update requires an actual connection.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ClinicBooking");
        if (string.IsNullOrWhiteSpace(connection)) options.UseSqlServer();
        else options.UseSqlServer(connection);
        return new BookingDbContext(options.Options);
    }
}
