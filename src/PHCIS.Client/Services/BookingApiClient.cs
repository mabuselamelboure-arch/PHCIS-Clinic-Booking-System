using System.Net;
using System.Net.Http.Json;
using PHCIS.Contracts;
namespace PHCIS.Client.Services;

public sealed class BookingApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
public sealed class BookingApiClient(HttpClient http)
{
    public async Task<List<ClinicResponse>> GetClinicsAsync() =>
        await ReadAsync<List<ClinicResponse>>(await http.GetAsync("api/clinics"));
    public async Task<List<TimeSlotResponse>> GetSlotsAsync(Guid clinicId, DateOnly date) =>
        await ReadAsync<List<TimeSlotResponse>>(await http.GetAsync($"api/clinics/{clinicId}/slots?date={date:yyyy-MM-dd}"));
    public async Task<BookingConfirmation> BookAsync(CreateBookingRequest request) =>
        await ReadAsync<BookingConfirmation>(await http.PostAsJsonAsync("api/appointments", request));
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        using (response)
        {
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("The server returned an empty response.");
            ApiProblem? problem = null;
            try { problem = await response.Content.ReadFromJsonAsync<ApiProblem>(); }
            catch (System.Text.Json.JsonException) { }
            var message = problem?.Errors is { Count: > 0 }
                ? string.Join(" ", problem.Errors.Values.SelectMany(x => x))
                : problem?.Title ?? "Unable to complete the request. Please try again.";
            throw new BookingApiException(message, response.StatusCode);
        }
    }
    private sealed class ApiProblem
    {
        public string? Title { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
