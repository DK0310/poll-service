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
        if (request.Questions is null || request.Questions.Count == 0)
            return Result<PollResponse>.Failure("At least one question is required");

        var questions = new List<Question>();
        for (var i = 0; i < request.Questions.Count; i++)
        {
            var q = request.Questions[i];
            var label = $"Question {i + 1}";

            if (string.IsNullOrWhiteSpace(q.Text))
                return Result<PollResponse>.Failure($"{label}: text is required");

            if (!Enum.TryParse<PollQuestionType>(q.Type, ignoreCase: true, out var type))
                return Result<PollResponse>.Failure($"{label}: invalid question type");

            // Options depend on the question type: SingleChoice uses the creator's options;
            // YesNo and Rating are server-generated; OpenText has no options.
            var optionsResult = BuildOptionTexts(type, q.Options);
            if (!optionsResult.IsSuccess)
                return Result<PollResponse>.Failure($"{label}: {optionsResult.Error!}");

            questions.Add(new Question
            {
                QuestionIndex = i,
                Text = q.Text.Trim(),
                Type = type,
                Options = optionsResult.Value!
                    .Select((text, j) => new PollOption { OptionIndex = j, Text = text })
                    .ToList()
            });
        }

        var code = await GenerateUniqueCodeAsync();
        var poll = new Poll
        {
            Code = code,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Status = PollStatus.Open,
            CreatorId = creatorId,
            ExpiresAt = request.ExpiryHours.HasValue
                ? DateTime.UtcNow.AddHours(request.ExpiryHours.Value)
                : null,
            Questions = questions
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
        if (poll is null)
            return Result<PollResponse>.Failure("Poll not found");

        // Lazy expiry: if the poll is past ExpiresAt but the background cleanup sweep
        // hasn't closed it yet (e.g. the instance was idle on a free-tier host), persist
        // the close now — so auto-close doesn't depend on the sweep being awake.
        if (poll.IsExpired && !poll.IsClosed)
        {
            poll.Status = PollStatus.Closed;
            await _repo.UpdateAsync(poll);
        }

        return Result<PollResponse>.Success(PollResponse.From(poll));
    }

    // isAdmin bypasses the creator check (admins moderate any poll).
    public async Task<Result<PollResponse>> CloseAsync(string code, Guid userId, bool isAdmin = false)
    {
        var poll = await _repo.GetByCodeAsync(code);
        if (poll is null) return Result<PollResponse>.Failure("Poll not found");
        if (!isAdmin && poll.CreatorId != userId) return Result<PollResponse>.Failure("Not the poll creator");
        if (poll.IsClosed) return Result<PollResponse>.Failure("Poll is already closed");

        poll.Status = PollStatus.Closed;
        await _repo.UpdateAsync(poll);
        return Result<PollResponse>.Success(PollResponse.From(poll));
    }

    public async Task<Result<bool>> DeleteAsync(string code, Guid userId, bool isAdmin = false)
    {
        var poll = await _repo.GetByCodeAsync(code);
        if (poll is null) return Result<bool>.Failure("Poll not found");
        if (!isAdmin && poll.CreatorId != userId) return Result<bool>.Failure("Not the poll creator");

        await _repo.DeleteAsync(poll);
        return Result<bool>.Success(true);
    }

    public async Task<IEnumerable<PollResponse>> GetByCreatorAsync(Guid userId)
    {
        var polls = await _repo.GetByCreatorAsync(userId);
        return polls.Select(PollResponse.From);
    }

    /// <summary>Every poll, newest first — for the admin dashboard.</summary>
    public async Task<IEnumerable<PollResponse>> GetAllAsync()
    {
        var polls = await _repo.GetAllAsync();
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
