using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PHCIS.Api.Data;
using PHCIS.Contracts;

namespace PHCIS.Api.Security;

public sealed record TokenSettings(byte[] Key, string Issuer, string Audience);

public static class Authentication
{
    public static void AddPatientAuthentication(this WebApplicationBuilder builder)
    {
        var configured = builder.Configuration["Jwt:SigningKey"];
        byte[] key;
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
                throw new InvalidOperationException("Configure Jwt__SigningKey with a random base64 key of at least 32 bytes.");
            key = RandomNumberGenerator.GetBytes(64); // Ephemeral development/test key, never a committed credential.
        }
        else
        {
            key = Convert.FromBase64String(configured);
            if (key.Length < 32) throw new InvalidOperationException("Jwt signing key must contain at least 32 random bytes.");
        }
        var settings = new TokenSettings(key, "PHCIS", "PHCIS.Client");
        builder.Services.AddSingleton(settings);
        builder.Services.AddIdentityCore<IdentityUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        }).AddEntityFrameworkStores<BookingDbContext>().AddSignInManager();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = settings.Issuer, ValidAudience = settings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(settings.Key), ClockSkew = TimeSpan.FromSeconds(15)
            };
        });
        builder.Services.AddAuthorization();
    }

    public static void MapPatientAuthentication(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (RegisterRequest request, UserManager<IdentityUser> users,
            TokenSettings settings) =>
        {
            if (!ValidCredentials(request.Email, request.Password)) return InvalidCredentials();
            var email = request.Email.Trim();
            var user = new IdentityUser { UserName = email, Email = email };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Account"] =
                    ["Account could not be created. Use a unique email and a password of at least 10 characters including upper/lowercase, a digit and a symbol."] });
            return Results.Ok(IssueToken(user, settings));
        }).RequireRateLimiting("accounts");

        app.MapPost("/api/auth/login", async (LoginRequest request, UserManager<IdentityUser> users,
            SignInManager<IdentityUser> signIn, TokenSettings settings) =>
        {
            if (!ValidCredentials(request.Email, request.Password)) return Results.Unauthorized();
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null) return Results.Unauthorized();
            var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            return result.Succeeded ? Results.Ok(IssueToken(user, settings)) : Results.Unauthorized();
        }).RequireRateLimiting("accounts");
    }

    private static bool ValidCredentials(string? email, string? password) =>
        email is { Length: > 0 and <= 254 } && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.Trim())
        && password is { Length: >= 10 and <= 128 };
    private static IResult InvalidCredentials() => Results.ValidationProblem(new Dictionary<string, string[]>
    { ["Account"] = ["Provide a valid email and a password of 10-128 characters."] });
    private static AuthResponse IssueToken(IdentityUser user, TokenSettings settings)
    {
        var now = DateTime.UtcNow;
        var expiry = now.AddMinutes(15);
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            now, expiry, new SigningCredentials(new SymmetricSecurityKey(settings.Key), SecurityAlgorithms.HmacSha256));
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(jwt), expiry, user.Email!);
    }
}
