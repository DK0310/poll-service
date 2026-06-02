using System.Net.Http.Json;

namespace VoteApi.Services;

/// <summary>
/// Typed HttpClient wrapper for the inter-service call to the Poll API.
/// Returns null on a non-2xx response or transport failure so callers degrade gracefully
/// (never accept a vote referencing an unvalidated poll).
/// Method is virtual so VoteService can be unit-tested with a mocked client.
/// </summary>
public class PollClientService
{
    private readonly HttpClient _http;
    public PollClientService(HttpClient http) => _http = http;

    public virtual async Task<PollInfo?> GetPollAsync(string code)
    {
        try
        {
            var response = await _http.GetAsync($"/api/polls/{code}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<PollInfo>();
        }
        catch (HttpRequestException)
        {
            return null; // Poll API unavailable
        }
    }
}

// Shape of the poll data this service consumes from the Poll API's PollResponse
// (subset — matched case-insensitively against the camelCase JSON).
public record PollInfo
{
    public string Code { get; init; } = "";
    public string Question { get; init; } = "";
    public bool IsActive { get; init; }
    public List<PollOptionInfo> Options { get; init; } = new();
}

public record PollOptionInfo
{
    public int OptionIndex { get; init; }
    public string Text { get; init; } = "";
}
