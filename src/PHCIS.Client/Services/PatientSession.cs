using System.Net.Http.Headers;
using System.Net.Http.Json;
using PHCIS.Contracts;
namespace PHCIS.Client.Services;

// Tokens live only in memory, never localStorage. Reload/expiry requires sign-in again.
public sealed class PatientSession(HttpClient http)
{
    public string? Email { get; private set; }
    private DateTime expiresUtc;
    public bool IsAuthenticated => Email is not null && expiresUtc > DateTime.UtcNow;
    public async Task SignInAsync(string email, string password, bool register)
    {
        using var response = await http.PostAsJsonAsync(register ? "api/auth/register" : "api/auth/login", new LoginRequest(email, password));
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(register
                ? "Account could not be created. Use a unique email and a password with at least 10 characters, upper/lowercase, a digit and a symbol."
                : "Sign-in failed. Check your details or wait five minutes if the account is locked.");
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>() ?? throw new InvalidOperationException("Missing sign-in response.");
        Email = auth.Email; expiresUtc = auth.ExpiresUtc;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }
    public void SignOut() { Email = null; expiresUtc = default; http.DefaultRequestHeaders.Authorization = null; }
}
