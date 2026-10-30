using FluentValidation;
namespace PHCIS.Contracts;

public sealed record ClinicResponse(Guid Id, string Name, string Address);
public sealed record TimeSlotResponse(Guid Id, Guid ClinicId, DateTime StartUtc, DateTime EndUtc);
public sealed record BookingConfirmation(string Reference, string ClinicName, string ClinicAddress,
    DateTime StartUtc, DateTime EndUtc);

public sealed class CreateBookingRequest
{
    public Guid? ClinicId { get; set; }
    public Guid TimeSlotId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
}

public sealed class BookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public BookingRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.TimeSlotId).NotEmpty().WithMessage("Select an available time slot.");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(25)
            .Matches(@"^\+?[0-9 ()-]{7,25}$").WithMessage("Use digits and common phone formatting only.")
            .Must(phone => phone is not null && phone.Count(char.IsAsciiDigit) is >= 7 and <= 15)
            .WithMessage("Enter a contact number containing 7-15 digits.");
    }
}

/// <summary>Assessment clinics operate in South African Standard Time (UTC+02:00).</summary>
public static class ClinicTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(2);
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Add(Offset));
    public static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Offset);
    public static DateTime ToUtc(DateOnly day, TimeOnly time) => DateTime.SpecifyKind(day.ToDateTime(time).Subtract(Offset), DateTimeKind.Utc);
}

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string AccessToken, DateTime ExpiresUtc, string Email);
