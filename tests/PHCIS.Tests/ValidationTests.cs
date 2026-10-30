using PHCIS.Contracts;
namespace PHCIS.Tests;

public sealed class ValidationTests
{
    private readonly BookingRequestValidator validator = new();
    [Fact]
    public void Valid_request_passes() => Assert.True(validator.Validate(Valid()).IsValid);
    [Fact]
    public void Missing_fields_fail()
    {
        var errors = validator.Validate(new CreateBookingRequest()).Errors.Select(e => e.PropertyName).ToHashSet();
        Assert.Contains(nameof(CreateBookingRequest.TimeSlotId), errors);
        Assert.Contains(nameof(CreateBookingRequest.FullName), errors);
        Assert.Contains(nameof(CreateBookingRequest.Email), errors);
        Assert.Contains(nameof(CreateBookingRequest.Phone), errors);
    }
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void Invalid_email_fails(string email)
    {
        var request = Valid(); request.Email = email;
        Assert.False(validator.Validate(request).IsValid);
    }
    [Theory]
    [InlineData("letters only")]
    [InlineData("123")]
    [InlineData("()---()()")]
    [InlineData("1234567890123456")]
    public void Invalid_phone_fails(string phone)
    {
        var request = Valid(); request.Phone = phone;
        Assert.False(validator.Validate(request).IsValid);
    }
    [Fact]
    public void Whitespace_name_fails()
    {
        var request = Valid(); request.FullName = "   ";
        Assert.False(validator.Validate(request).IsValid);
    }
    [Fact]
    public void Oversized_fields_fail()
    {
        var request = Valid(); request.FullName = new string('x', 121);
        request.Email = new string('x', 250) + "@test.example";
        request.Phone = new string('1', 26);
        Assert.Equal(3, validator.Validate(request).Errors.Select(e => e.PropertyName).Distinct().Count());
    }
    [Fact]
    public void Clinic_date_rolls_over_at_South_African_midnight()
    {
        var clock = new FixedClock(new DateTimeOffset(2030, 1, 7, 22, 30, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2030, 1, 8), ClinicTime.Today(clock));
        Assert.Equal(new DateTime(2030, 1, 7, 22, 0, 0, DateTimeKind.Utc), ClinicTime.ToUtc(new DateOnly(2030, 1, 8), TimeOnly.MinValue));
    }
    internal static CreateBookingRequest Valid(Guid? slotId = null) => new()
    {
        TimeSlotId = slotId ?? Guid.NewGuid(), FullName = "Alex Example", Email = "alex@example.test", Phone = "+27 82 000 0000"
    };
}
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
