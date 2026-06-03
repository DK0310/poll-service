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

        if (!Enum.TryParse<PollQuestionType>(request.Type, ignoreCase: true, out var type))
            return Result<PollResponse>.Failure("Invalid question type");

        // Options depend on the question type: SingleChoice uses the creator's options;
        // YesNo and Rating are server-generated; OpenText has no options.
        var optionsResult = BuildOptionTexts(type, request.Options);
        if (!optionsResult.IsSuccess)
            return Result<PollResponse>.Failure(optionsResult.Error!);

        var code = await GenerateUniqueCodeAsync();
        var poll = new Poll
        {
            Code = code,
            Question = request.Question.Trim(),
            Type = type,
            Status = PollStatus.Open,
            CreatorId = creatorId,
            ExpiresAt = request.ExpiryHours.HasValue
                ? DateTime.UtcNow.AddHours(request.ExpiryHours.Value)
                : null,
            Options = optionsResult.Value!
                .Select((text, i) => new PollOption { OptionIndex = i, Text = text })
                .ToList()
        };

        await _repo.AddAsync(poll);
        return Result<PollResponse>.Success(PollResponse.From(poll));
    }

    private static Result<List<string>> BuildOptionTexts(PollQuestionType type, List<string>? options)
    {
        switch (type)
        {
            case PollQuestionType.YesNo:
                return Result<List<string>>.Success(["Yes", "No"]);
            case PollQuestionType.Rating:
                return Result<List<string>>.Success(["1", "2", "3", "4", "5"]);
            case PollQuestionType.OpenText:
                return Result<List<string>>.Success([]);
            default: // SingleChoice
                if (options is null || options.Count < 2)
                    return Result<List<string>>.Failure("At least 2 options are required");
                if (options.Count > 6)
                    return Result<List<string>>.Failure("Maximum 6 options allowed");
                if (options.Any(string.IsNullOrWhiteSpace))
                    return Result<List<string>>.Failure("Option text cannot be empty");
                return Result<List<string>>.Success(options.Select(o => o.Trim()).ToList());
        }
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

    /// <summary>
    /// Closes every poll whose expiry has passed (called by the background cleanup service).
    /// Returns the number of polls closed.
    /// </summary>
    public async Task<int> CloseExpiredPollsAsync()
    {
        var expired = await _repo.GetExpiredAsync();
        var count = 0;
        foreach (var poll in expired)
        {
            poll.Status = PollStatus.Closed;
            await _repo.UpdateAsync(poll);
            count++;
        }
        return count;
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
