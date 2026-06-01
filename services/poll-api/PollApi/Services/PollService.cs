using System.Security.Cryptography;
using PollApi.Common;
using PollApi.DTOs;
using PollApi.Models;
using PollApi.Repositories;

namespace PollApi.Services;

public class PollService
{
    private const int CodeLength = 5;
    private const string CodeChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly PollRepository _repo;
    public PollService(PollRepository repo) => _repo = repo;

    public async Task<Result<PollResponse>> CreateAsync(CreatePollRequest request, Guid? creatorId)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Result<PollResponse>.Failure("Question is required");
        if (request.Options is null || request.Options.Count < 2)
            return Result<PollResponse>.Failure("At least 2 options are required");
        if (request.Options.Count > 6)
            return Result<PollResponse>.Failure("Maximum 6 options allowed");
        if (request.Options.Any(string.IsNullOrWhiteSpace))
            return Result<PollResponse>.Failure("Option text cannot be empty");

        var code = await GenerateUniqueCodeAsync();
        var poll = new Poll
        {
            Code = code,
            Question = request.Question.Trim(),
            Status = PollStatus.Open,
            CreatorId = creatorId,
            ExpiresAt = request.ExpiryHours.HasValue
                ? DateTime.UtcNow.AddHours(request.ExpiryHours.Value)
                : null,
            Options = request.Options
                .Select((text, i) => new PollOption { OptionIndex = i, Text = text.Trim() })
                .ToList()
        };

        await _repo.AddAsync(poll);
        return Result<PollResponse>.Success(PollResponse.From(poll));
    }

    public async Task<Result<PollResponse>> GetByCodeAsync(string code)
    {
        var poll = await _repo.GetByCodeAsync(code);
        return poll is null
            ? Result<PollResponse>.Failure("Poll not found")
            : Result<PollResponse>.Success(PollResponse.From(poll));
    }

    public async Task<Result<PollResponse>> CloseAsync(string code, Guid userId)
    {
        var poll = await _repo.GetByCodeAsync(code);
        if (poll is null) return Result<PollResponse>.Failure("Poll not found");
        if (poll.CreatorId != userId) return Result<PollResponse>.Failure("Not the poll creator");
        if (poll.IsClosed) return Result<PollResponse>.Failure("Poll is already closed");

        poll.Status = PollStatus.Closed;
        await _repo.UpdateAsync(poll);
        return Result<PollResponse>.Success(PollResponse.From(poll));
    }

    public async Task<Result<bool>> DeleteAsync(string code, Guid userId)
    {
        var poll = await _repo.GetByCodeAsync(code);
        if (poll is null) return Result<bool>.Failure("Poll not found");
        if (poll.CreatorId != userId) return Result<bool>.Failure("Not the poll creator");

        await _repo.DeleteAsync(poll);
        return Result<bool>.Success(true);
    }

    public async Task<IEnumerable<PollResponse>> GetByCreatorAsync(Guid userId)
    {
        var polls = await _repo.GetByCreatorAsync(userId);
        return polls.Select(PollResponse.From);
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        string code;
        do
        {
            code = new string(Enumerable.Range(0, CodeLength)
                .Select(_ => CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)])
                .ToArray());
        }
        while (await _repo.GetByCodeAsync(code) is not null);
        return code;
    }
}
