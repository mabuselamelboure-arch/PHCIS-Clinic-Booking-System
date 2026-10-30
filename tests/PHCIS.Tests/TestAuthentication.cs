using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PHCIS.Api.Data;
namespace PHCIS.Tests;

internal static class TestAuthentication
{
    public static void Add(IServiceCollection services) => services.AddAuthentication("TestPatient")
        .AddScheme<AuthenticationSchemeOptions, TestPatientHandler>("TestPatient", _ => { });
}
internal sealed class TestPatientHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-patient")], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
