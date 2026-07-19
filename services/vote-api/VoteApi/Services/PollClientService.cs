using System.Net.Http.Json;

namespace VoteApi.Services;

/// <summary>
/// Calls poll-api over HTTP to look up a poll. Returns null on any non-2xx or transport error, so
/// a caller that can't confirm the poll simply refuses the vote instead of trusting stale data.
/// GetPollAsync is virtual so VoteService's unit tests can mock it.
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

// The subset of poll-api's PollResponse that vote-api actually needs. Bound case-insensitively
// against the camelCase JSON, so field names don't have to match casing exactly.
public record PollInfo
{
    public string Code { get; init; } = "";
    public string? Title { get; init; }
    public bool IsActive { get; init; }
    public Guid? CreatorId { get; init; }   // owner — for analytics/pin ownership checks
    public List<PollQuestionInfo> Questions { get; init; } = new();
}

public record PollQuestionInfo
{
    public Guid Id { get; init; }
    public int QuestionIndex { get; init; }
    public string Text { get; init; } = "";
    public string Type { get; init; } = "SingleChoice";
    public List<PollOptionInfo> Options { get; init; } = new();
}

public record PollOptionInfo
{
    public int OptionIndex { get; init; }
    public string Text { get; init; } = "";
}
